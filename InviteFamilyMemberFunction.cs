using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCR_AI_Grocery.models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocery
{
    public class InviteFamilyMemberFunction
    {
        private readonly ILogger _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;
        private readonly Container _familyMembersContainer;

        public InviteFamilyMemberFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ProcessReceiptOCR>();
            string cosmosDbConnection = Environment.GetEnvironmentVariable("CosmosDBConnectionString") ?? string.Empty;
            _cosmosClient = new CosmosClient(cosmosDbConnection);
            _container = _cosmosClient.GetContainer("ReceiptsDB", "FamilyInvites");
            _familyMembersContainer = _cosmosClient.GetContainer("ReceiptsDB", "FamilyJunction");
        }
        [Function("InviteFamilyMember")]
        public async Task<IActionResult> InviteFamilyMember(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/{familyId}/inviteMember")] HttpRequest req,
    string FamilyId)
        {
            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonConvert.DeserializeObject<dynamic>(requestBody);

                string invitedUserEmail = data?.email ?? string.Empty;
                string invitedBy = data?.invitedBy ?? string.Empty;
                if (string.IsNullOrEmpty(invitedUserEmail) || string.IsNullOrEmpty(invitedBy))
                    return new BadRequestObjectResult(new { message = "Email and inviter details are required." });

                var query = new QueryDefinition("SELECT * FROM c WHERE c.FamilyId = @familyId AND c.invitedUserEmail = @invitedUserEmail")
                    .WithParameter("@familyId", FamilyId)
                    .WithParameter("@invitedUserEmail", invitedUserEmail.ToLower());

                using var queryIterator = _container.GetItemQueryIterator<dynamic>(query);
                while (queryIterator.HasMoreResults)
                {
                    var response = await queryIterator.ReadNextAsync();
                    if (response.Count > 0)
                        return new ConflictObjectResult(new { message = "Invite already sent." });
                }

                var invite = new
                {
                    id = Guid.NewGuid().ToString(),
                    FamilyId,
                    invitedUserEmail = invitedUserEmail.ToLower(),
                    invitedBy = invitedBy.ToLower(),
                    status = "pending"
                };

                await _container.CreateItemAsync(invite, new PartitionKey(FamilyId));
                return new OkObjectResult(new { message = "Invitation sent!", inviteId = invite.id });
            }
            catch (CosmosException ex)
            {
                _logger.LogError($"CosmosDB Error: {ex.Message}");
                return new StatusCodeResult((int)ex.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending invite: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }


        [Function("GetPendingInvites")]
        public async Task<IActionResult> GetPendingInvites(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "family/invites/{email}")] HttpRequest req,
    string email,
    ILogger log)
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.invitedUserEmail = @email AND c.status = 'pending'")
                        .WithParameter("@email", email);

            using FeedIterator<dynamic> resultSet = _container.GetItemQueryIterator<dynamic>(query);

            var invites = new List<dynamic>();
            while (resultSet.HasMoreResults)
            {
                foreach (var invite in await resultSet.ReadNextAsync())
                {
                    invites.Add(invite);
                }
            }

            return new OkObjectResult(invites);
        }

        [Function("RejectFamilyInvite")]
        public async Task<IActionResult> RejectFamilyInvite(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/invites/{inviteId}/reject")] HttpRequest req,
    string inviteId,
    ILogger log)
        {
            var response = await _container.ReadItemAsync<dynamic>(inviteId, new PartitionKey(inviteId));
            var invite = response.Resource;

            if (invite == null || invite.status != "pending")
            {
                return new BadRequestObjectResult(new { message = "Invalid or expired invitation." });
            }

            invite.status = "rejected";
            await _container.ReplaceItemAsync(invite, inviteId, new PartitionKey(inviteId));

            return new OkObjectResult(new { message = "Invitation rejected." });
        }


        [Function("AcceptFamilyInvite")]
        public async Task<IActionResult> AcceptFamilyInvite(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/invites/{inviteId}/accept")] HttpRequest req,
        string inviteId,
        ILogger log)
        {
            log.LogInformation($"Processing invitation acceptance for Invite ID: {inviteId}");

            try
            {
                var response = await _container.ReadItemAsync<FamilyInvite>(inviteId, new PartitionKey(inviteId));
                var invite = response.Resource;

                if (invite == null || invite.Status != "pending")
                {
                    log.LogWarning($"Invalid or expired invite: {inviteId}");
                    return new BadRequestObjectResult(new { message = "Invalid or expired invitation." });
                }

                // Create a new family member
                var newMember = new FamilyMember
                {
                    MemberId = Guid.NewGuid().ToString(),
                    FamilyId = invite.FamilyId,
                    Email = invite.InvitedUserEmail,
                    Name = invite.InvitedUserEmail.Split('@')[0], // Placeholder, should get actual name
                    PhotoURL = "", // Can be fetched from user profile
                    CreatedAt = DateTime.UtcNow
                };

                await _familyMembersContainer.CreateItemAsync(newMember, new PartitionKey(invite.FamilyId));

                // Update invite status to "accepted"
                invite.Status = "accepted";
                await _container.ReplaceItemAsync(invite, inviteId, new PartitionKey(inviteId));

                log.LogInformation($"Invitation accepted. New member added: {JsonConvert.SerializeObject(newMember)}");

                return new OkObjectResult(new
                {
                    message = "Invitation accepted! You are now a family member.",
                    familyId = invite.FamilyId,
                    memberId = newMember.MemberId,
                    email = newMember.Email,
                    name = newMember.Name
                });
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                log.LogError($"Invite not found: {inviteId}");
                return new NotFoundObjectResult(new { message = "Invitation not found." });
            }
            catch (Exception ex)
            {
                log.LogError($"Error accepting invite: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }



    }
}
