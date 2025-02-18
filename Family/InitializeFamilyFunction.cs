using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCR_AI_Grocery.Family.models;
using OCR_AI_Grocery.models;
using System;
using System.Threading.Tasks;

namespace OCR_AI_Grocery
{
    public class InitializeFamilyFunction
    {
        private readonly ILogger _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _familyContainer;
        private readonly Container _familyJunctionContainer;

        public InitializeFamilyFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<InitializeFamilyFunction>();
            string cosmosDbConnection = Environment.GetEnvironmentVariable("CosmosDBConnectionString") ?? string.Empty;
            _cosmosClient = new CosmosClient(cosmosDbConnection);
            _familyContainer = _cosmosClient.GetContainer("ReceiptsDB", "Families");
            _familyJunctionContainer = _cosmosClient.GetContainer("ReceiptsDB", "FamilyJunction");
        }

        [Function("InitializeFamily")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/initialize")] HttpRequest req)
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonConvert.DeserializeObject<InitializeFamilyRequest>(requestBody);

                if (string.IsNullOrEmpty(data?.Email))
                {
                    return new BadRequestObjectResult(new { message = "Email is required." });
                }

                var email = data.Email.ToLower();
                _logger.LogInformation($"Checking family status for user: {email}");

                // First, check FamilyJunction for existing membership
                var existingFamilyId = await CheckExistingFamilyMembership(email);
                if (!string.IsNullOrEmpty(existingFamilyId))
                {
                    _logger.LogInformation($"Found existing family membership for {email}: {existingFamilyId}");
                    return new OkObjectResult(new
                    {
                        familyId = existingFamilyId,
                        isNew = false
                    });
                }

                // Then check if user has a family where they are the primary contact
                var existingPrimaryFamily = await CheckExistingPrimaryFamily(email);
                if (existingPrimaryFamily != null)
                {
                    _logger.LogInformation($"Found existing primary family for {email}: {existingPrimaryFamily.FamilyId}");

                    // Ensure junction exists
                    await EnsureFamilyJunction(email, existingPrimaryFamily.FamilyId);

                    return new OkObjectResult(new
                    {
                        familyId = existingPrimaryFamily.FamilyId,
                        isNew = false
                    });
                }

                // If no existing family found, create new one
                var newFamilyId = Guid.NewGuid().ToString();
                var family = new FamilyEntity
                {
                    Id = newFamilyId,
                    FamilyId = newFamilyId,
                    FamilyName = $"{email.Split('@')[0]}'s Family",
                    PrimaryEmail = email
                };

                await _familyContainer.CreateItemAsync(family, new PartitionKey(family.FamilyId));

                // Create family junction
                var junction = new FamilyJunction
                {
                    Id = email,
                    Email = email,
                    FamilyId = newFamilyId
                };

                await _familyJunctionContainer.CreateItemAsync(junction, new PartitionKey(newFamilyId));

                _logger.LogInformation($"Created new family with ID {newFamilyId} for {email}");

                return new OkObjectResult(new
                {
                    familyId = newFamilyId,
                    isNew = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initializing family: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        private async Task<string> CheckExistingFamilyMembership(string email)
        {
            var query = new QueryDefinition(
                "SELECT c.FamilyId FROM c WHERE c.email = @email")
                .WithParameter("@email", email);

            using var iterator = _familyJunctionContainer.GetItemQueryIterator<dynamic>(query);

            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                if (response.Count > 0)
                {
                    return response.First().FamilyId.ToString();
                }
            }

            return null;
        }

        private async Task<FamilyEntity> CheckExistingPrimaryFamily(string email)
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.PrimaryEmail = @email")
                .WithParameter("@email", email);

            using var iterator = _familyContainer.GetItemQueryIterator<FamilyEntity>(query);

            if (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                return response.FirstOrDefault();
            }

            return null;
        }

        private async Task EnsureFamilyJunction(string email, string familyId)
        {
            try
            {
                var junction = new FamilyJunction
                {
                    Id = email,
                    Email = email,
                    FamilyId = familyId
                };

                await _familyJunctionContainer.CreateItemAsync(junction, new PartitionKey(familyId));
                _logger.LogInformation($"Created missing junction for existing family: {email} -> {familyId}");
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // Junction already exists, which is fine
                _logger.LogInformation($"Junction already exists for: {email} -> {familyId}");
            }
        }
    }
}