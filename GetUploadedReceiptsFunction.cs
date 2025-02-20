using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using OCR_AI_Grocery.models;

namespace OCR_AI_Grocery
{
    public class GetUploadedReceiptsFunction
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;
        private readonly ILogger<GetUploadedReceiptsFunction> _logger;
        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 50;

        public GetUploadedReceiptsFunction(ILogger<GetUploadedReceiptsFunction> logger)
        {
            string cosmosDbConnection = Environment.GetEnvironmentVariable("CosmosDBConnectionString") ?? string.Empty;
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
                _logger.LogInformation("📥 Fetching paginated list of receipts...");

                // Extract query parameters
                var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                string? userEmail = queryParams["email"]?.ToLower();
                string? continuationToken = queryParams["continuationToken"];

                // Parse and validate page size
                int pageSize = DefaultPageSize;
                if (int.TryParse(queryParams["pageSize"], out int requestedPageSize))
                {
                    pageSize = Math.Min(Math.Max(1, requestedPageSize), MaxPageSize);
                }

                // Build the query
                QueryDefinition query;
                if (!string.IsNullOrEmpty(userEmail))
                {
                    query = new QueryDefinition("SELECT * FROM c WHERE c.UserId = @userEmail ORDER BY c._ts DESC")
                        .WithParameter("@userEmail", userEmail);
                }
                else
                {
                    query = new QueryDefinition("SELECT * FROM c ORDER BY c._ts DESC");
                }

                // Set query options with pagination
                var queryOptions = new QueryRequestOptions
                {
                    MaxItemCount = pageSize
                };

                // Initialize response object
                var response = new
                {
                    Items = new List<Receipt>(),
                    ContinuationToken = "",
                    PageSize = pageSize,
                    HasMoreResults = false
                };

                // Execute query with continuation token if provided
                using (FeedIterator<Receipt> queryIterator = _container.GetItemQueryIterator<Receipt>(
                    query,
                    continuationToken,
                    queryOptions))
                {
                    if (queryIterator.HasMoreResults)
                    {
                        FeedResponse<Receipt> currentPage = await queryIterator.ReadNextAsync();

                        response = new
                        {
                            Items = currentPage.ToList(),
                            ContinuationToken = currentPage.ContinuationToken,
                            PageSize = pageSize,
                            HasMoreResults = queryIterator.HasMoreResults || !string.IsNullOrEmpty(currentPage.ContinuationToken)
                        };
                    }
                }

                _logger.LogInformation($"✅ Retrieved {response.Items.Count} receipts.");
                return new OkObjectResult(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error retrieving receipts: {ex.Message}");
                return new ObjectResult(new { error = "Internal Server Error" }) { StatusCode = 500 };
            }
        }
    }
}