using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OCR_AI_Grocery.models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCR_AI_Grocery
{
    public class GetFamilyInvitesFunction
    {
        private readonly ILogger _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _invitesContainer;

        public GetFamilyInvitesFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<GetFamilyInvitesFunction>();
            string cosmosDbConnection = Environment.GetEnvironmentVariable("CosmosDBConnectionString") ?? string.Empty;
            _cosmosClient = new CosmosClient(cosmosDbConnection);
            _invitesContainer = _cosmosClient.GetContainer("ReceiptsDB", "FamilyInvites");
        }

        [Function("GetFamilyInvites")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "family/invites/{email}")] HttpRequest req,
            string email)
        {
            try
            {
                _logger.LogInformation($"Fetching invites for email: {email}");

                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.InvitedUserEmail = @email AND c.status = 'pending'")
                    .WithParameter("@email", email.ToLower());

                var invites = new List<FamilyInvite>();
                using var iterator = _invitesContainer.GetItemQueryIterator<FamilyInvite>(query);

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    invites.AddRange(response);
                }

                _logger.LogInformation($"Found {invites.Count} pending invites for {email}");
                return new OkObjectResult(invites);
            }
            catch (CosmosException ex)
            {
                _logger.LogError($"Cosmos DB Error: {ex.Message}");
                return new StatusCodeResult((int)ex.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching invites: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}