using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OCR_AI_Grocery.Models;
using OCR_AI_Grocey.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Repos
{
    public class ShoppingListRepository : IShoppingListRepository
    {
        private readonly Container _shoppingListContainer;
        private readonly ILogger<ShoppingListRepository> _logger;
        private readonly string _partitionKeyPath;

        public ShoppingListRepository(
            CosmosClient cosmosClient,
            ILoggerFactory loggerFactory)
        {
            _shoppingListContainer = cosmosClient.GetContainer("ReceiptsDB", "ShoppingLists");
            _logger = loggerFactory.CreateLogger<ShoppingListRepository>();

            try
            {
                // Read the container's actual partition key path
                var containerProperties = _shoppingListContainer.ReadContainerAsync().GetAwaiter().GetResult();
                _partitionKeyPath = containerProperties.Resource.PartitionKeyPath;
                _logger.LogInformation($"ShoppingLists container partition key path: {_partitionKeyPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error reading container properties: {ex.Message}");
                // Default to a likely partition key path
                _partitionKeyPath = "/UserId";
                _logger.LogInformation($"Using default partition key path: {_partitionKeyPath}");
            }
        }

        public async Task<ShoppingList> GetExistingShoppingList(string familyId)
        {
            try
            {
                _logger.LogInformation($"Fetching shopping list for family {familyId}"); 

                // Extract partition key property name
                string pkPropertyName = _partitionKeyPath.TrimStart('/');

                // Create a dynamic query based on the partition key property name
                var queryText = $"SELECT * FROM c WHERE c.{pkPropertyName} = @familyId";
                var queryDef = new QueryDefinition(queryText)
                    .WithParameter("@familyId", familyId);

                _logger.LogInformation($"Using query: {queryText}");

                // Execute the query
                var iterator = _shoppingListContainer.GetItemQueryIterator<ShoppingList>(queryDef);
                var results = new List<ShoppingList>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response);
                }

                // If we found a shopping list, return it
                if (results.Count > 0)
                {
                    _logger.LogInformation($"Found existing shopping list for family {familyId}");
                    return results[0];
                }

                // Otherwise create a new one
                var newList = new ShoppingList
                {
                    Id = familyId,
                    UserId = familyId,
                    StoreItems = new Dictionary<string, List<string>>(),
                    CreatedAt = DateTime.UtcNow
                };

                _logger.LogInformation($"Creating new shopping list for family {familyId}");

                // Save the new list
                await UpdateShoppingList(newList);

                return newList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to fetch shopping list for family {familyId}");
                throw;
            }
        }

        public async Task<List<ShoppingList>> GetShoppingListsForFamily(string userEmail)
        {
            try
            {
                _logger.LogInformation($"Fetching all shopping lists for family ID: {userEmail}");

                // Extract the partition key property name from the path
                string pkPropertyName = _partitionKeyPath.TrimStart('/');

                // Create a dynamic query that uses the correct partition key property name
                var queryText = $"SELECT * FROM c WHERE c.{pkPropertyName} = @email";
                var shoppingListQuery = new QueryDefinition(queryText)
                    .WithParameter("@email", userEmail);

                _logger.LogInformation($"Executing query: {queryText} with parameter @email = {userEmail}");

                // Execute the query
                var shoppingLists = new List<ShoppingList>();
                using (FeedIterator<ShoppingList> iterator = _shoppingListContainer.GetItemQueryIterator<ShoppingList>(shoppingListQuery))
                {
                    while (iterator.HasMoreResults)
                    {
                        FeedResponse<ShoppingList> response = await iterator.ReadNextAsync();
                        _logger.LogInformation($"Retrieved {response.Count} shopping lists in this batch");
                        shoppingLists.AddRange(response);
                    }
                }

                _logger.LogInformation($"Retrieved a total of {shoppingLists.Count} shopping lists for family: {userEmail}");
                return shoppingLists;
            }
            catch (CosmosException cex)
            {
                _logger.LogError($"Cosmos DB Error: {cex.StatusCode} - {cex.SubStatusCode}");
                _logger.LogError($"Message: {cex.Message}");
                if (cex.ResponseBody != null)
                {
                    _logger.LogError($"Response: {cex.ResponseBody}");
                }
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching shopping lists: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task UpdateShoppingList(ShoppingList shoppingList)
        {
            try
            {
                if (string.IsNullOrEmpty(shoppingList.Id))
                    throw new ArgumentException("Shopping List ID cannot be null or empty");

                if (string.IsNullOrEmpty(shoppingList.UserId))
                    throw new ArgumentException("User ID cannot be null or empty");

                _logger.LogInformation($"Updating shopping list for family {shoppingList.UserId}");

                // Use the same approach that worked in the ReceiptService
                var serializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore
                };

                // First serialize to string with our settings
                var json = JsonConvert.SerializeObject(shoppingList, serializerSettings);

                // Then parse back to JObject for manipulation
                var listJObject = JObject.Parse(json);

                // Extract partition key name from path (remove the leading slash)
                string partitionKeyName = _partitionKeyPath.TrimStart('/');

                // Get the partition key value from the JObject
                JToken partitionKeyToken = listJObject[partitionKeyName];

                // If the property doesn't exist with the expected name, try case-insensitive search
                if (partitionKeyToken == null)
                {
                    foreach (var prop in listJObject.Properties())
                    {
                        if (string.Equals(prop.Name, partitionKeyName, StringComparison.OrdinalIgnoreCase))
                        {
                            partitionKeyToken = prop.Value;

                            // Rename the property to match the partition key path exactly
                            prop.Remove();
                            listJObject[partitionKeyName] = partitionKeyToken;

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
                    partitionKeyValue = shoppingList.UserId;
                    listJObject[partitionKeyName] = partitionKeyValue;
                    _logger.LogWarning($"No partition key found in JSON. Using UserId: {partitionKeyValue}");
                }

                // Create the partition key
                var partitionKey = new PartitionKey(partitionKeyValue);

                // Create options to bypass serialization issues
                var options = new ItemRequestOptions
                {
                    EnableContentResponseOnWrite = false
                };

                // Update directly using the JObject
                var response = await _shoppingListContainer.UpsertItemAsync<JObject>(
                    listJObject,
                    partitionKey,
                    options);

                _logger.LogInformation($"Successfully updated shopping list for family {shoppingList.UserId}. Request charge: {response.RequestCharge} RUs");
            }
            catch (CosmosException cex)
            {
                _logger.LogError($"Cosmos DB Error updating shopping list: {cex.StatusCode} - {cex.SubStatusCode}");
                _logger.LogError($"Message: {cex.Message}");
                if (cex.ResponseBody != null)
                {
                    _logger.LogError($"Response: {cex.ResponseBody}");
                }
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to update shopping list for family {shoppingList.UserId}");
                throw;
            }
        }
    }
}