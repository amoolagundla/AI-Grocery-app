﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCR_AI_Grocery.Family.models;
using OCR_AI_Grocery.models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Container = Microsoft.Azure.Cosmos.Container;
using FamilyInvite = OCR_AI_Grocery.models.FamilyInvite;

namespace OCR_AI_Grocery
{
    public class FamilyFunctions
    {
        private readonly ILogger _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _familyContainer;
        private readonly Container _familyJunctionContainer;
        private readonly Container _invitesContainer;

        public FamilyFunctions(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<FamilyFunctions>();
            string cosmosDbConnection = Environment.GetEnvironmentVariable("CosmosDBConnectionString") ?? string.Empty;
            _cosmosClient = new CosmosClient(cosmosDbConnection);

            _familyContainer = _cosmosClient.GetContainer("ReceiptsDB", "Families");
            _familyJunctionContainer = _cosmosClient.GetContainer("ReceiptsDB", "FamilyJunction");
            _invitesContainer = _cosmosClient.GetContainer("ReceiptsDB", "FamilyInvites");
        }

        // GET /api/family/byEmail/{email}
        [Function("GetFamiliesByEmail")]
        public async Task<IActionResult> GetFamiliesByEmail(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "family/byEmail/{email}")] HttpRequest req,
            string email)
        {
            try
            {
                _logger.LogInformation($"Fetching families for email: {email}");

                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.InvitedUserEmail = @email")
                    .WithParameter("@email", email.ToLower());

                var families = new List<FamilyEntity>();
                using var iterator = _familyJunctionContainer.GetItemQueryIterator<FamilyJunction>(query);

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    foreach (var junction in response)
                    {
                        try
                        {
                            var family = await _familyContainer.ReadItemAsync<FamilyEntity>(
                                junction.FamilyId,
                                new PartitionKey(junction.FamilyId)
                            );
                            families.Add(new FamilyEntity
                            {
                                Id = family.Resource.Id,
                                FamilyName = family.Resource.FamilyName,
                                PrimaryEmail = family.Resource.PrimaryEmail
                            });
                        }
                        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            _logger.LogWarning($"Family not found for junction: {junction.FamilyId}");
                            continue;
                        }
                    }
                }

                return new OkObjectResult(families);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching families: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }


        // POST /api/family/{familyId}/inviteMember
        [Function("InviteFamilyMember")]
        public async Task<IActionResult> InviteFamilyMember(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/{familyId}/inviteMember")] HttpRequest req,
            string familyId)
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonConvert.DeserializeObject<dynamic>(requestBody);

                string invitedUserEmail = data?.email?.ToString().ToLower() ?? string.Empty;
                string invitedBy = data?.invitedBy?.ToString().ToLower() ?? string.Empty;

                if (string.IsNullOrEmpty(invitedUserEmail) || string.IsNullOrEmpty(invitedBy))
                {
                    return new BadRequestObjectResult(new { message = "Email and inviter details are required." });
                }

                // Check for existing pending invite
                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.InvitedUserEmail = @email AND c.familyId = @familyId AND c.status = 'pending'")
                    .WithParameter("@email", invitedUserEmail)
                    .WithParameter("@familyId", familyId);

                using var iterator = _invitesContainer.GetItemQueryIterator<FamilyInvite>(query);
                var response = await iterator.ReadNextAsync();

                if (response.Count > 0)
                {
                    return new ConflictObjectResult(new { message = "Pending invitation already exists." });
                }

                var invite = new FamilyInvite
                {
                     
                    FamilyId = familyId,
                    InvitedUserEmail = invitedUserEmail,
                    InvitedId = invitedBy,
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                };

                await _invitesContainer.UpsertItemAsync(
                   invite,
                   new PartitionKey(invite.FamilyId)
               );
                return new OkObjectResult(new { message = "Invitation sent successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending invite: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [Function("ProcessFamilyInvite")]
        public async Task<IActionResult> ProcessInvite(
     [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/invites/{inviteId}/{invitedUserEmail}/process")] HttpRequest req,
     string inviteId, string invitedUserEmail)
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonConvert.DeserializeObject<dynamic>(requestBody);
                string action = data?.action?.ToString().ToLower() ?? "";

                if (string.IsNullOrEmpty(action) || (action != "accept" && action != "reject"))
                {
                    return new BadRequestObjectResult(new { message = "Invalid action. Must be 'accept' or 'reject'." });
                }

                // Find the invite
                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.id = @inviteId and c.InvitedUserEmail=@invitedUserEmail and c.Status = 'pending'")
                    .WithParameter("@inviteId", inviteId)
                    .WithParameter("@invitedUserEmail", invitedUserEmail);

                var invites = new List<FamilyInvite>();
                using var iterator = _invitesContainer.GetItemQueryIterator<FamilyInvite>(query);
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    invites.AddRange(response);
                }

                if (!invites.Any())
                {
                    return new NotFoundObjectResult(new { message = "Invite not found." });
                }

                var invite = invites.First();

                if (action == "accept")
                {
                    // Check if junction already exists
                    var junctionQuery = new QueryDefinition(
                        "SELECT * FROM c WHERE c.Email = @email AND c.FamilyId = @familyId")
                        .WithParameter("@email", invite.InvitedUserEmail)
                        .WithParameter("@familyId", invite.FamilyId);

                    var existingJunctions = new List<FamilyJunction>();
                    using var junctionIterator = _familyJunctionContainer.GetItemQueryIterator<FamilyJunction>(junctionQuery);
                    while (junctionIterator.HasMoreResults)
                    {
                        var response = await junctionIterator.ReadNextAsync();
                        existingJunctions.AddRange(response);
                    }

                    if (!existingJunctions.Any())
                    {
                        var junction = new FamilyJunction
                        {
                            Id = Guid.NewGuid().ToString(),
                            Email = invite.InvitedUserEmail,
                            FamilyId = invite.FamilyId,
                            JoinDate = DateTime.UtcNow,
                            Status = "Active",
                            PartitionKey = invite.FamilyId  // Make sure this matches your model
                        };

                        await _familyJunctionContainer.CreateItemAsync(
                            junction,
                            new PartitionKey(junction.PartitionKey)
                        );
                    }
                }

                // Update the invite
                invite.Status = action == "accept" ? "accepted" : "rejected";
                invite.ResponseDate = DateTime.UtcNow;
                invite.PartitionKey = invitedUserEmail;  // Make sure this matches your model

               
                await _invitesContainer.UpsertItemAsync(
                    invite, 
                    new PartitionKey(invite.FamilyId)
                );

                return new OkObjectResult(new
                {
                    message = $"Invitation {action}ed successfully.",
                    familyId = invite.FamilyId
                });
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogError($"CosmosDB Not Found Error: {ex.Message}");
                return new NotFoundObjectResult(new { message = "Invite not found." });
            }
            catch (CosmosException ex)
            {
                _logger.LogError($"CosmosDB Error: {ex.Message}");
                return new StatusCodeResult((int)ex.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing invite: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [Function("GetFamilyDetails")]
        public async Task<IActionResult> Run(
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "family/{id}")] HttpRequest req,
           string id,
           ILogger log)
        {
            try
            {
                QueryDefinition query = new QueryDefinition(
           "SELECT * FROM c WHERE c.id = @id")
           .WithParameter("@id", id);

                var families = new List<FamilyEntity>();

                using var iterator = _familyContainer.GetItemQueryIterator<FamilyJunction>(query);
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    foreach (var junction in response)
                    {
                        try
                        {
                            var family = await _familyContainer.ReadItemAsync<FamilyEntity>(
                                junction.FamilyId,
                                new PartitionKey(junction.FamilyId)
                            );

                            families.Add(new FamilyEntity
                            {
                                Id = family.Resource.Id,
                                FamilyName = family.Resource.FamilyName,
                                PrimaryEmail = family.Resource.PrimaryEmail
                            });
                        }
                        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            log.LogWarning($"Family not found for junction: {junction.FamilyId}");
                            continue;
                        }
                    }
                }

                return new OkObjectResult(families);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new NotFoundObjectResult($"Family with ID {id} not found");
            }
            catch (Exception ex)
            {
                log.LogError($"Error retrieving family: {ex.Message}");
                return new StatusCodeResult(500);
            }
        }
    }
}