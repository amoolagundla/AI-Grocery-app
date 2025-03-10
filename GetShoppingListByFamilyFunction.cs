using Azure.Messaging.ServiceBus;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OCR_AI_Grocery.models;
using OCR_AI_Grocery.Models;

namespace OCR_AI_Grocery
{
    public class GetShoppingListByFamilyFunction
    {
        private readonly ILogger _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container; 
        private QueueClient _queueClient; 
        private readonly ServiceBusSender _queueSender;
        private readonly ServiceBusClient _serviceBusClient;
        private readonly Container _receiptsContainer;
        private readonly Container _shoppingListsContainer;

        public GetShoppingListByFamilyFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ProcessReceiptOCR>();
            string cosmosDbConnection = Environment.GetEnvironmentVariable("CosmosDBConnectionString") ?? string.Empty;
            _cosmosClient = new CosmosClient(cosmosDbConnection);
            _container = _cosmosClient.GetContainer("ReceiptsDB", "receipts");
            _receiptsContainer = _cosmosClient.GetContainer("ReceiptsDB", "receipts");
            _shoppingListsContainer = _cosmosClient.GetContainer("ReceiptsDB", "ShoppingLists");
            string serviceBusConnectionString = Environment.GetEnvironmentVariable("QueueConnectionString")
                ?? throw new InvalidOperationException("Service Bus connection string not found");

            // Create Service Bus client and sender
            _serviceBusClient = new ServiceBusClient(serviceBusConnectionString);
            _queueSender = _serviceBusClient.CreateSender("receipt-analysis-queue");

        }

        [Function("GetShoppingListsForFamily")]
        public async Task<IActionResult> Run(
         [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "family/{userEmail}/shoppingLists")] HttpRequestData req,
         string userEmail)
        {
            _logger.LogInformation($"Fetching receipts for FamilyId: {userEmail}");

            try
            { 
                // Step 2: Query ShoppingLists for extracted emails
                var shoppingLists = new List<dynamic>();
                var shoppingListQuery = new QueryDefinition("SELECT * FROM c WHERE c.UserId = @email")
                                             .WithParameter("@email", userEmail);

                using (FeedIterator<ShoppingList> iterator = _shoppingListsContainer.GetItemQueryIterator<ShoppingList>(shoppingListQuery))
                {
                    while (iterator.HasMoreResults)
                    {
                        FeedResponse<ShoppingList> response = await iterator.ReadNextAsync();
                        shoppingLists.AddRange(response);
                    }
                }

                if (!shoppingLists.Any())
                {
                    return new NotFoundObjectResult(new { message = "No shopping lists found for this family." });
                }

                return new OkObjectResult(shoppingLists);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching shopping lists: {ex.Message}");
                return new ObjectResult(new { message = "Error retrieving shopping lists", error = ex.Message }) { StatusCode = 500 };
            }
        }
    }
}
