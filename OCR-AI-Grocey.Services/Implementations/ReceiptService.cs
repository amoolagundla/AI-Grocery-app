using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using OCR_AI_Grocery.Models.Receipt;
using OCR_AI_Grocey.Services.Interfaces;

namespace OCR_AI_Grocey.Services.Implementations
{
    public class ReceiptService : IReceiptService
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;
        private readonly ILogger<ReceiptService> _logger;
        private readonly string _partitionKeyPath;

        public ReceiptService(CosmosClient cosmosClient, ILogger<ReceiptService> logger)
        {
            _cosmosClient = cosmosClient;
            _logger = logger;

            try
            {
                var database = _cosmosClient.GetDatabase("ReceiptsDB");
                _container = database.GetContainer("receipts");

                // Read the actual partition key path from the container
                var containerProperties = _container.ReadContainerAsync().GetAwaiter().GetResult();
                _partitionKeyPath = containerProperties.Resource.PartitionKeyPath;

                _logger.LogInformation($"Container partition key path: {_partitionKeyPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initializing container: {ex.Message}");
                throw;
            }
        }

        public async Task SaveReceiptAsync(ReceiptDocument receipt)
        {
            try
            {
                _logger.LogInformation($"Attempting to save receipt with FamilyId: {receipt.FamilyId}");

                // Convert to JObject to have direct control over the JSON structure
                // Use the serializer settings that match our JsonProperty attributes
                var serializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore
                };

                // First serialize to string with our settings
                var json = JsonConvert.SerializeObject(receipt, serializerSettings);

                // Then parse back to JObject for manipulation if needed
                var receiptJObject = JObject.Parse(json);

                // Log the full JSON document that will be sent
                _logger.LogInformation($"Document to be saved: {receiptJObject.ToString(Formatting.Indented)}");

                // Extract partition key name from path (remove the leading slash)
                string partitionKeyName = _partitionKeyPath.TrimStart('/');

                // Get the partition key value from the JObject 
                JToken partitionKeyToken = receiptJObject[partitionKeyName];

                if (partitionKeyToken == null)
                {
                    // If the property doesn't exist with the expected name, try case-insensitive search
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

                // If we still don't have a partition key value, use a default
                string partitionKeyValue;
                if (partitionKeyToken != null)
                {
                    partitionKeyValue = partitionKeyToken.ToString();
                }
                else
                {
                    partitionKeyValue = "default";
                    receiptJObject[partitionKeyName] = partitionKeyValue;
                    _logger.LogWarning($"No partition key found. Added default value: {partitionKeyValue}");
                }

                // Ensure the document has an id
                if (receiptJObject["id"] == null)
                {
                    receiptJObject["id"] = Guid.NewGuid().ToString();
                }

                _logger.LogInformation($"Using partition key: {partitionKeyName}={partitionKeyValue}");

                // Create the partition key with the extracted value
                var partitionKey = new PartitionKey(partitionKeyValue);

                // Create options to bypass serialization issues
                var options = new ItemRequestOptions
                {
                    EnableContentResponseOnWrite = false
                };

                // Save directly using the JObject instead of the original receipt object
                // This bypasses any serialization inconsistencies in the SDK
                var response = await _container.CreateItemAsync<JObject>(
                    receiptJObject,
                    partitionKey,
                    options);

                _logger.LogInformation($"Successfully saved receipt. Request charge: {response.RequestCharge} RUs");
            }
            catch (CosmosException cex)
            {
                _logger.LogError($"Cosmos DB Error: {cex.StatusCode} - {cex.SubStatusCode}");
                _logger.LogError($"Message: {cex.Message}");
                _logger.LogError($"Response: {cex.ResponseBody}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving receipt: {ex.Message}");
                throw;
            }
        }
    }
}