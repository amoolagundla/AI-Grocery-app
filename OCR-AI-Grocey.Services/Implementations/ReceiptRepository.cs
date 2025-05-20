using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OCR_AI_Grocery.Models.Receipt;
using OCR_AI_Grocey.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Implementations
{
    public class ReceiptRepository : IReceiptRepository
    {
        private readonly Container _receiptsContainer;
        private readonly ILogger<ReceiptRepository> _logger;
        private readonly string _partitionKeyPath;

        public ReceiptRepository(
            CosmosClient cosmosClient,
            ILoggerFactory loggerFactory)
        {
            _receiptsContainer = cosmosClient.GetContainer("ReceiptsDB", "receipts");
            _logger = loggerFactory.CreateLogger<ReceiptRepository>();

            try
            {
                // Read the container's actual partition key path
                var containerProperties = _receiptsContainer.ReadContainerAsync().GetAwaiter().GetResult();
                _partitionKeyPath = containerProperties.Resource.PartitionKeyPath;
                _logger.LogInformation($"Container partition key path: {_partitionKeyPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error reading container properties: {ex.Message}");
                // Default to what we know is the partition key path
                _partitionKeyPath = "/FamilyId";
                _logger.LogInformation($"Using default partition key path: {_partitionKeyPath}");
            }
        }
        public async Task<List<ReceiptDocument>> FetchRecipet(string receiptId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(receiptId))
                    throw new ArgumentException("Receipt ID cannot be null or empty", nameof(receiptId));

                var query = new QueryDefinition(
                  "SELECT * FROM c WHERE c.UserId = @userEmail")
                  .WithParameter("@userEmail", receiptId);

                _logger.LogInformation("Querying Cosmos DB for receiptId: {ReceiptId}", receiptId);

                var receipts = new List<ReceiptDocument>();
                using FeedIterator<ReceiptDocument> queryIterator = _receiptsContainer.GetItemQueryIterator<ReceiptDocument>(query);

                while (queryIterator.HasMoreResults)
                {
                    FeedResponse<ReceiptDocument> response = await queryIterator.ReadNextAsync();
                    receipts.AddRange(response);
                }

                _logger.LogInformation("Found {Count} receipts with id {ReceiptId}", receipts.Count, receiptId);
                return receipts;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Cosmos DB error fetching receipt with id {ReceiptId}: {StatusCode}", receiptId, ex.StatusCode);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error fetching receipt with id {ReceiptId}", receiptId);
                throw;
            }
        }

        public async Task<List<ReceiptDocument>> FetchReceipts()
        {
            try
            {


                var query = new QueryDefinition(
                  "SELECT * FROM c ");

                _logger.LogInformation("Querying Cosmos DB ");

                var receipts = new List<ReceiptDocument>();
                using FeedIterator<ReceiptDocument> queryIterator = _receiptsContainer.GetItemQueryIterator<ReceiptDocument>(query);

                while (queryIterator.HasMoreResults)
                {
                    FeedResponse<ReceiptDocument> response = await queryIterator.ReadNextAsync();
                    receipts.AddRange(response);
                }

                 
                return receipts;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Cosmos DB error fetching receipt with id  : {StatusCode}",   ex.StatusCode);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error fetching receipt with id  "  );
                throw;
            }
        }


        public async Task<List<ReceiptDocument>> FetchUnprocessedReceipts(string familyId)
        {
            try
            {
                // Make sure to use the exact property name from the container's partition key path
                string pkPropertyName = _partitionKeyPath.TrimStart('/');

                var query = new QueryDefinition(
                     $"SELECT * FROM c WHERE c.{pkPropertyName} = @familyId AND (c.Processed = false OR NOT IS_DEFINED(c.Processed))")
                     .WithParameter("@familyId", familyId);

                _logger.LogInformation($"Querying with: {query.QueryText}");

                var receipts = new List<ReceiptDocument>();
                using FeedIterator<ReceiptDocument> queryIterator = _receiptsContainer.GetItemQueryIterator<ReceiptDocument>(query);

                while (queryIterator.HasMoreResults)
                {
                    FeedResponse<ReceiptDocument> response = await queryIterator.ReadNextAsync();
                    receipts.AddRange(response);
                }

                _logger.LogInformation($"Found {receipts.Count} unprocessed receipts for {familyId}");
                return receipts;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching unprocessed receipts: {ex.Message}");
                throw;
            }
        }

        

        public async Task UpdateReceipt(ReceiptDocument receipt)
        {
            try
            {
                _logger.LogInformation($"Attempting to update receipt {receipt.Id} for family {receipt.FamilyId}");

                // Use the same approach that worked in the ReceiptService
                var serializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore
                };

                // First serialize to string with our settings
                var json = JsonConvert.SerializeObject(receipt, serializerSettings);

                // Then parse back to JObject for manipulation
                var receiptJObject = JObject.Parse(json);

                // Extract partition key name from path (remove the leading slash)
                string partitionKeyName = _partitionKeyPath.TrimStart('/');

                // Get the partition key value from the JObject
                JToken partitionKeyToken = receiptJObject[partitionKeyName];

                // If the property doesn't exist with the expected name, try case-insensitive search
                if (partitionKeyToken == null)
                {
                    foreach (var prop in receiptJObject.Properties())
                    {
                        if (string.Equals(prop.Name, partitionKeyName, StringComparison.OrdinalIgnoreCase))
                        {
                            partitionKeyToken = prop.Value;

                            // Rename the property to match the partition key path exactly
                            prop.Remove();
                            receiptJObject[partitionKeyName] = partitionKeyToken;

                            _logger.LogInformation($"Renamed property {prop.Name} to {partitionKeyName}");
                            break;
                        }
                    }
                }

                // Get the partition key value
                string partitionKeyValue;
                if (partitionKeyToken != null)
                {
                    partitionKeyValue = partitionKeyToken.ToString();
                }
                else
                {
                    partitionKeyValue = receipt.FamilyId ?? "default";
                    receiptJObject[partitionKeyName] = partitionKeyValue;
                    _logger.LogWarning($"No partition key found in JSON. Using value from object: {partitionKeyValue}");
                }

                // Create the partition key
                var partitionKey = new PartitionKey(partitionKeyValue);

                // Create options to bypass serialization issues
                var options = new ItemRequestOptions
                {
                    EnableContentResponseOnWrite = false
                };

                // Update directly using the JObject
                var response = await _receiptsContainer.UpsertItemAsync<JObject>(
                    receiptJObject,
                    partitionKey,
                    options);

                _logger.LogInformation($"Successfully updated receipt {receipt.Id}. Request charge: {response.RequestCharge} RUs");
            }
            catch (CosmosException cex)
            {
                _logger.LogError($"Cosmos DB Error updating receipt: {cex.StatusCode} - {cex.SubStatusCode}");
                _logger.LogError($"Message: {cex.Message}");
                if (cex.ResponseBody != null)
                {
                    _logger.LogError($"Response: {cex.ResponseBody}");
                }
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating receipt: {ex.Message}");
                throw;
            }
        }
    }
}