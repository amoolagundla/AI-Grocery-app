using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;

namespace OCR_AI_Grocery.Tokens
{
    public class SavePushTokenFunction
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;
        private readonly ILogger _logger; 

        public SavePushTokenFunction(ILoggerFactory logger)
        {
            string cosmosDbConnection = Environment.GetEnvironmentVariable("CosmosDBConnectionString") ?? string.Empty;
            _cosmosClient = new CosmosClient(cosmosDbConnection);
            _container = _cosmosClient.GetContainer("ReceiptsDB", "Tokens");
            _logger = logger.CreateLogger<SavePushTokenFunction>();
        }

        [Function("SavePushToken")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "push-token")] HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("📱 Processing push token save request...");

                // Read and validate request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var tokenRequest = JsonSerializer.Deserialize<PushTokenRequest>(requestBody);

                if (string.IsNullOrEmpty(tokenRequest?.UserEmail) || string.IsNullOrEmpty(tokenRequest?.Token))
                {
                    return new BadRequestObjectResult(new { error = "Email and token are required" });
                }

                // Normalize email
                tokenRequest.UserEmail = tokenRequest.UserEmail.ToLowerInvariant();

                // Check if token already exists using partition key
                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.Token = @token")
                    .WithParameter("@token", tokenRequest.Token);

                var queryOptions = new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(tokenRequest.UserEmail)
                };

                using var iterator = _container.GetItemQueryIterator<PushTokenDocument>(
                    query,
                    requestOptions: queryOptions
                );

                if (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    if (response.Count > 0)
                    {
                        // Update existing token's LastUpdated
                        var existingToken = response.First();
                        existingToken.LastUpdated = DateTime.UtcNow;
                        await _container.ReplaceItemAsync(
                            existingToken,
                            existingToken.id,
                            new PartitionKey(existingToken.UserEmail)
                        );

                        _logger.LogInformation($"✅ Updated existing token for {tokenRequest.UserEmail}");
                        return new OkObjectResult(new { message = "Token updated successfully" });
                    }
                }

                // Create new token document
                var tokenDoc = new PushTokenDocument
                {
                    id = Guid.NewGuid().ToString(),
                    UserEmail = tokenRequest.UserEmail,
                    Token = tokenRequest.Token,
                    Platform = tokenRequest.Platform ?? "native",
                    CreatedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow
                };

                await _container.CreateItemAsync(
                    tokenDoc,
                    new PartitionKey(tokenDoc.UserEmail)
                );

                _logger.LogInformation($"✅ Saved new push token for {tokenRequest.UserEmail}");
                return new OkObjectResult(new { message = "Token saved successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error saving push token: {ex.Message}");
                return new ObjectResult(new { error = "Internal Server Error" }) { StatusCode = 500 };
            }
        }
    }
}