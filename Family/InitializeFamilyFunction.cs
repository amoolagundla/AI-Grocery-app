using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCR_AI_Grocery.Family.models; 

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
                _logger.LogInformation($"Initializing family for user: {email}");

                // Check if user already has a family
                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.Email = @email")
                    .WithParameter("@email", email);

                using var iterator = _familyJunctionContainer.GetItemQueryIterator<FamilyJunction>(query);
                var response = await iterator.ReadNextAsync();

                if (response.Count > 0)
                {
                    var existingFamily = response.First();
                    return new OkObjectResult(new
                    {
                        familyId = existingFamily.FamilyId,
                        isNew = false
                    });
                }

                // Create new family
                var familyId = Guid.NewGuid().ToString();
                var family = new FamilyEntity
                {
                    Id = familyId,
                    FamilyName = $"{email.Split('@')[0]}'s Family",
                    PrimaryEmail = email
                };

                await _familyContainer.CreateItemAsync(family, new PartitionKey(familyId));

                // Create family junction
                var junction = new FamilyJunction
                {
                    Id = email,
                    Email = email,
                    FamilyId = familyId
                };

                await _familyJunctionContainer.CreateItemAsync(junction, new PartitionKey(familyId));

                _logger.LogInformation($"Created new family with ID {familyId} for {email}");

                return new OkObjectResult(new
                {
                    familyId = familyId,
                    isNew = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initializing family: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    } 
}