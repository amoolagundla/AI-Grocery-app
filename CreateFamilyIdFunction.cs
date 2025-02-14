using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace OCR_AI_Grocery
{
    public class CreateFamilyIdFunction
    {
        private readonly ILogger<CreateFamilyIdFunction> _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;

        public CreateFamilyIdFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<CreateFamilyIdFunction>();

            // Initialize Cosmos DB Client
            string? cosmosDbConnection = Environment.GetEnvironmentVariable("CosmosDBConnectionString");
            if (string.IsNullOrEmpty(cosmosDbConnection))
            {
                throw new InvalidOperationException("❌ CosmosDB connection string is not configured.");
            }

            _cosmosClient = new CosmosClient(cosmosDbConnection);
            _container = _cosmosClient.GetContainer("ReceiptsDB", "FamilyJunction");
        }

        [Function("CreateFamilyId")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            try
            {
                // Read the request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var requestData = JsonSerializer.Deserialize<CreateFamilyRequest>(requestBody);

                if (requestData == null || string.IsNullOrWhiteSpace(requestData.Email))
                {
                    return CreateErrorResponse(req, "Invalid request. 'Email' is required.", HttpStatusCode.BadRequest);
                }

                string email = requestData.Email.ToLower();

                // Check if email already exists in Cosmos DB
                var existingFamily = await GetFamilyByEmail(email);
                if (existingFamily != null)
                {
                    return CreateSuccessResponse(req, new { FamilyId = existingFamily.FamilyId });
                }

                // Generate a new FamilyId
                string newFamilyId = Guid.NewGuid().ToString();

                // Save the new FamilyId and Email to Cosmos DB
                var familyRecord = new FamilyJunction
                {
                    Id = email,
                    FamilyId = newFamilyId,
                    Email = email
                };

                await _container.CreateItemAsync(familyRecord, new PartitionKey(familyRecord.FamilyId));
                _logger.LogInformation($"✅ New FamilyId created: {newFamilyId} for Email: {email}");

                return CreateSuccessResponse(req, new { FamilyId = newFamilyId });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error processing request: {ex.Message}");
                return CreateErrorResponse(req, "Internal Server Error", HttpStatusCode.InternalServerError);
            }
        }

        private async Task<FamilyJunction?> GetFamilyByEmail(string email)
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.Email = @email")
                .WithParameter("@email", email);

            using FeedIterator<FamilyJunction> queryIterator = _container.GetItemQueryIterator<FamilyJunction>(query);

            while (queryIterator.HasMoreResults)
            {
                FeedResponse<FamilyJunction> response = await queryIterator.ReadNextAsync();
                foreach (var item in response)
                {
                    return item; // Return first match
                }
            }

            return null;
        }

        private HttpResponseData CreateSuccessResponse(HttpRequestData req, object data)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            response.WriteString(JsonSerializer.Serialize(data));
            return response;
        }

        private HttpResponseData CreateErrorResponse(HttpRequestData req, string message, HttpStatusCode statusCode)
        {
            var response = req.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "application/json");
            response.WriteString(JsonSerializer.Serialize(new { Error = message }));
            return response;
        }
    }

    public class CreateFamilyRequest
    {
        public string? Email { get; set; }
    }

    public class FamilyJunction
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty; // Email as ID

        [JsonPropertyName("FamilyId")]
        public string FamilyId { get; set; } = string.Empty;

        [JsonPropertyName("Email")]
        public string Email { get; set; } = string.Empty;
    }
}
