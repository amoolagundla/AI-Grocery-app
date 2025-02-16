using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OCR_AI_Grocery.models;

namespace OCR_AI_Grocery
{
    public class GetUploadedReceiptsFunction
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;
        private readonly ILogger<GetUploadedReceiptsFunction> _logger;

        public GetUploadedReceiptsFunction(  ILogger<GetUploadedReceiptsFunction> logger)
        {
            string cosmosDbConnection = Environment.GetEnvironmentVariable("CosmosDBConnectionString")??String.Empty;
            _cosmosClient = new CosmosClient(cosmosDbConnection);
            _container = _cosmosClient.GetContainer("ReceiptsDB", "receipts");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [Function("GetUploadedReceipts")]
        public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "receipts")] HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("📥 Fetching list of uploaded receipts...");

                // Extract query parameters (optional filtering)
                var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                string? userEmail = queryParams["email"]?.ToLower(); // Convert email to lowercase for consistency

                var receipts = new List<Receipt>();

                QueryDefinition query;
                if (!string.IsNullOrEmpty(userEmail))
                {
                    // ✅ Filter receipts for a specific user
                    query = new QueryDefinition("SELECT * FROM c WHERE c.UserId = @userEmail ORDER BY c._ts DESC")
                                                .WithParameter("@userEmail", userEmail);
                }
                else
                {
                    // ✅ Get all receipts
                    query = new QueryDefinition("SELECT * FROM c");
                }

                using FeedIterator<Receipt> queryIterator = _container.GetItemQueryIterator<Receipt>(query);

                while (queryIterator.HasMoreResults)
                {
                    FeedResponse<Receipt> response = await queryIterator.ReadNextAsync();
                    receipts.AddRange(response);
                }

                _logger.LogInformation($"✅ Retrieved {receipts.Count} receipts.");
                return new OkObjectResult(receipts);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error retrieving receipts: {ex.Message}");
                return new ObjectResult(new { error = "Internal Server Error" }) { StatusCode = 500 };
            }
        } 
    } 
    
}
