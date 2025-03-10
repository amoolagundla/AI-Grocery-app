using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OCR_AI_Grocery.Family.models; 
using OCR_AI_Grocery.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.Services.Repositories
{
    public class FamilyRepository : IFamilyRepository
    {
        private readonly ILogger<FamilyRepository> _logger;
        private readonly Container _familyContainer;
        private readonly Container _familyJunctionContainer;
        private readonly Container _invitesContainer;

        private readonly string _familyPkPath;
        private readonly string _junctionPkPath;
        private readonly string _invitePkPath;

        public FamilyRepository(
            CosmosClient cosmosClient,
            ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<FamilyRepository>();

            _familyContainer = cosmosClient.GetContainer("ReceiptsDB", "Families");
            _familyJunctionContainer = cosmosClient.GetContainer("ReceiptsDB", "FamilyJunction");
            _invitesContainer = cosmosClient.GetContainer("ReceiptsDB", "FamilyInvites");

            try
            {
                // Read container partition key paths
                var familyProps = _familyContainer.ReadContainerAsync().GetAwaiter().GetResult();
                _familyPkPath = familyProps.Resource.PartitionKeyPath;

                var junctionProps = _familyJunctionContainer.ReadContainerAsync().GetAwaiter().GetResult();
                _junctionPkPath = junctionProps.Resource.PartitionKeyPath;

                var inviteProps = _invitesContainer.ReadContainerAsync().GetAwaiter().GetResult();
                _invitePkPath = inviteProps.Resource.PartitionKeyPath;

                _logger.LogInformation($"Family container PK path: {_familyPkPath}");
                _logger.LogInformation($"Junction container PK path: {_junctionPkPath}");
                _logger.LogInformation($"Invite container PK path: {_invitePkPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error reading container properties: {ex.Message}");
                // Default to likely partition key paths
                _familyPkPath = "/id";
                _junctionPkPath = "/FamilyId";
                _invitePkPath = "/FamilyId";
            }
        }

        public async Task<InitializeFamilyResponse> InitializeFamily(string email)
        {
            try
            {
                email = email.ToLower();
                _logger.LogInformation($"Initializing family for user: {email}");

                // First, check FamilyJunction for existing membership
                var existingFamilyId = await CheckExistingFamilyMembership(email);
                if (!string.IsNullOrEmpty(existingFamilyId))
                {
                    _logger.LogInformation($"Found existing family membership for {email}: {existingFamilyId}");
                    return new InitializeFamilyResponse
                    {
                        FamilyId = existingFamilyId,
                        IsNew = false
                    };
                }

                // Then check if user has a family where they are the primary contact
                var existingPrimaryFamily = await CheckExistingPrimaryFamily(email);
                if (existingPrimaryFamily != null)
                {
                    _logger.LogInformation($"Found existing primary family for {email}: {existingPrimaryFamily.Id}");

                    // Ensure junction exists
                    await EnsureFamilyJunction(email, existingPrimaryFamily.Id);

                    return new InitializeFamilyResponse
                    {
                        FamilyId = existingPrimaryFamily.Id,
                        IsNew = false
                    };
                }

                // If no existing family found, create new one
                var newFamilyId = await CreateNewFamily(email);

                return new InitializeFamilyResponse
                {
                    FamilyId = newFamilyId,
                    IsNew = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initializing family: {ex.Message}");
                throw;
            }
        }

        public async Task<string> CheckExistingFamilyMembership(string email)
        {
            try
            {
                _logger.LogInformation($"Checking existing family membership for: {email}");

                var query = new QueryDefinition(
                    "SELECT TOP 1 c.familyId FROM c WHERE c.Email = @email ORDER BY c._ts DESC")
                    .WithParameter("@email", email.ToLower());

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
            catch (Exception ex)
            {
                _logger.LogError($"Error checking existing family membership: {ex.Message}");
                throw;
            }
        }

        public async Task<FamilyEntity> CheckExistingPrimaryFamily(string email)
        {
            try
            {
                _logger.LogInformation($"Checking if user is primary contact for any family: {email}");

                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.primaryEmail = @email")
                    .WithParameter("@email", email.ToLower());

                using var iterator = _familyContainer.GetItemQueryIterator<FamilyEntity>(query);

                if (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    return response.FirstOrDefault();
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking existing primary family: {ex.Message}");
                throw;
            }
        }

        public async Task<string> CreateNewFamily(string email)
        {
            try
            {
                _logger.LogInformation($"Creating new family for user: {email}");

                var newFamilyId = Guid.NewGuid().ToString();
                var family = new FamilyEntity
                {
                    Id = newFamilyId,
                    FamilyId = newFamilyId,
                    FamilyName = $"{email.Split('@')[0]}'s Family",
                    PrimaryEmail = email.ToLower()
                };

                // Use the JSON approach for partition key safety
                var familyPkProperty = _familyPkPath.TrimStart('/');
                var serializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore
                };

                var json = JsonConvert.SerializeObject(family, serializerSettings);
                var familyJObject = JObject.Parse(json);

                // Ensure the partition key property exists with the correct name
                familyJObject[familyPkProperty] = newFamilyId;

                // Create the family
                await _familyContainer.CreateItemAsync<JObject>(
                    familyJObject,
                    new PartitionKey(newFamilyId)
                );

                // Create family junction
                var junction = new FamilyJunction
                {
                    Id = email,
                    Email = email.ToLower(),
                    FamilyId = newFamilyId,
                    PartitionKey = newFamilyId,
                    JoinDate = DateTime.UtcNow,
                    Status = "Active"
                };

                await CreateFamilyJunction(junction);

                _logger.LogInformation($"Created new family with ID {newFamilyId} for {email}");

                return newFamilyId;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating new family: {ex.Message}");
                throw;
            }
        }

        private async Task EnsureFamilyJunction(string email, string familyId)
        {
            try
            {
                _logger.LogInformation($"Ensuring junction exists for: {email} -> {familyId}");

                // Check if junction already exists
                bool junctionExists = await CheckFamilyJunctionExists(email, familyId);

                if (!junctionExists)
                {
                    var junction = new FamilyJunction
                    {
                        Id = email,
                        Email = email.ToLower(),
                        FamilyId = familyId,
                        PartitionKey = familyId,
                        JoinDate = DateTime.UtcNow,
                        Status = "Active"
                    };

                    await CreateFamilyJunction(junction);
                    _logger.LogInformation($"Created missing junction for existing family: {email} -> {familyId}");
                }
                else
                {
                    _logger.LogInformation($"Junction already exists for: {email} -> {familyId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error ensuring family junction: {ex.Message}");
                throw;
            }
        }

        public async Task<List<FamilyEntity>> GetFamiliesByEmail(string email)
        {
            try
            {
                _logger.LogInformation($"Fetching families for email: {email}");

                // Get the junction partition key property name
                string junctionPkProperty = _junctionPkPath.TrimStart('/');
                string userEmailProperty = "InvitedUserEmail"; // Adjust if needed based on schema

                var query = new QueryDefinition(
                    $"SELECT * FROM c WHERE c.{userEmailProperty} = @email")
                    .WithParameter("@email", email.ToLower());

                var families = new List<FamilyEntity>();
                using var iterator = _familyJunctionContainer.GetItemQueryIterator<FamilyJunction>(query);

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    foreach (var junction in response)
                    {
                        try
                        {
                            // Safely get the family ID regardless of property casing
                            string familyId = GetPropertyValueCaseInsensitive(junction, "FamilyId");

                            // Get the family partition key property value
                            string familyPkProperty = _familyPkPath.TrimStart('/');
                            var family = await ReadItemSafelyAsync<FamilyEntity>(
                                _familyContainer,
                                familyId,
                                familyId, // Assuming the PK value is the same as the ID
                                familyPkProperty
                            );

                            if (family != null)
                            {
                                families.Add(new FamilyEntity
                                {
                                    Id = family.Id,
                                    FamilyName = family.FamilyName,
                                    PrimaryEmail = family.PrimaryEmail
                                });
                            }
                        }
                        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            _logger.LogWarning($"Family not found for junction: {junction.FamilyId}");
                            continue;
                        }
                    }
                }

                return families;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching families: {ex.Message}");
                throw;
            }
        }

        public async Task<FamilyEntity> GetFamilyById(string id)
        {
            try
            {
                _logger.LogInformation($"Fetching family with ID: {id}");

                // Get the family partition key property name
                string familyPkProperty = _familyPkPath.TrimStart('/');

                // Try direct read first
                try
                {
                    var family = await ReadItemSafelyAsync<FamilyEntity>(
                        _familyContainer,
                        id,
                        id, // Assuming the PK value is the same as the ID
                        familyPkProperty
                    );

                    if (family != null)
                    {
                        return family;
                    }
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning($"Family with ID {id} not found via direct lookup");
                }

                // Fall back to query
                var queryText = $"SELECT * FROM c WHERE c.id = @id";
                var query = new QueryDefinition(queryText)
                    .WithParameter("@id", id);

                using var iterator = _familyContainer.GetItemQueryIterator<FamilyEntity>(query);

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    if (response.Count > 0)
                    {
                        return response.First();
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving family: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> CheckForPendingInvite(string email, string familyId)
        {
            try
            {
                _logger.LogInformation($"Checking for pending invite for email: {email} in family: {familyId}");

                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.invitedUserEmail = @email AND c.FamilyId = @familyId AND c.status = 'pending'")
                    .WithParameter("@email", email.ToLower())
                    .WithParameter("@familyId", familyId);

                using var iterator = _invitesContainer.GetItemQueryIterator<FamilyInvite>(query);
                var response = await iterator.ReadNextAsync();

                return response.Count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking for pending invite: {ex.Message}");
                throw;
            }
        }

        public async Task<List<FamilyInvite>> GetPendingInvitesByEmail(string email)
        {
            try
            {
                _logger.LogInformation($"Fetching pending invites for email: {email}");

                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.invitedUserEmail = @email AND c.status = 'pending'")
                    .WithParameter("@email", email.ToLower());

                var invites = new List<FamilyInvite>();
                using var iterator = _invitesContainer.GetItemQueryIterator<FamilyInvite>(query);

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    invites.AddRange(response);
                }

                _logger.LogInformation($"Found {invites.Count} pending invites for {email}");
                return invites;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching pending invites by email: {ex.Message}");
                throw;
            }
        }

        public async Task CreateFamilyInvite(FamilyInvite invite)
        {
            try
            {
                _logger.LogInformation($"Creating invite for email: {invite.InvitedUserEmail} in family: {invite.FamilyId}");

                // Get the invite container PK property name
                string invitePkProperty = _invitePkPath.TrimStart('/');

                // Use the JSON manipulation approach for safe partition key handling
                var serializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore
                };

                var json = JsonConvert.SerializeObject(invite, serializerSettings);
                var inviteJObject = JObject.Parse(json);

                // Ensure the partition key property exists with the correct name
                string pkValue = invite.FamilyId;
                inviteJObject[invitePkProperty] = pkValue;

                // Save with the correct partition key
                await _invitesContainer.UpsertItemAsync<JObject>(
                    inviteJObject,
                    new PartitionKey(pkValue)
                );

                _logger.LogInformation($"Successfully created invite for {invite.InvitedUserEmail}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating family invite: {ex.Message}");
                throw;
            }
        }

        public async Task<List<FamilyInvite>> GetPendingInvites(string inviteId, string invitedUserEmail)
        {
            try
            {
                _logger.LogInformation($"Finding pending invite with ID: {inviteId} for email: {invitedUserEmail}");

                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.id = @inviteId AND c.invitedUserEmail = @invitedUserEmail AND c.status = 'pending'")
                    .WithParameter("@inviteId", inviteId)
                    .WithParameter("@invitedUserEmail", invitedUserEmail);

                var invites = new List<FamilyInvite>();
                using var iterator = _invitesContainer.GetItemQueryIterator<FamilyInvite>(query);

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    invites.AddRange(response);
                }

                return invites;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting pending invites: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> CheckFamilyJunctionExists(string email, string familyId)
        {
            try
            {
                _logger.LogInformation($"Checking if junction exists for email: {email} in family: {familyId}");

                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.Email = @email AND c.FamilyId = @familyId")
                    .WithParameter("@email", email.ToLower())
                    .WithParameter("@familyId", familyId);

                var existingJunctions = new List<FamilyJunction>();
                using var iterator = _familyJunctionContainer.GetItemQueryIterator<FamilyJunction>(query);

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    existingJunctions.AddRange(response);
                }

                return existingJunctions.Any();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking family junction: {ex.Message}");
                throw;
            }
        }

        public async Task CreateFamilyJunction(FamilyJunction junction)
        {
            try
            {
                _logger.LogInformation($"Creating junction for email: {junction.Email} in family: {junction.FamilyId}");

                // Get the junction container PK property name
                string junctionPkProperty = _junctionPkPath.TrimStart('/');

                // Use the JSON manipulation approach for safe partition key handling
                var serializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore
                };

                var json = JsonConvert.SerializeObject(junction, serializerSettings);
                var junctionJObject = JObject.Parse(json);

                // Ensure the partition key property exists with the correct name
                string pkValue = junction.PartitionKey;
                junctionJObject[junctionPkProperty] = pkValue;

                // Save with the correct partition key
                await _familyJunctionContainer.CreateItemAsync<JObject>(
                    junctionJObject,
                    new PartitionKey(pkValue)
                );

                _logger.LogInformation($"Successfully created junction for {junction.Email}");
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // Junction already exists, which is fine
                _logger.LogInformation($"Junction already exists for: {junction.Email} -> {junction.FamilyId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating family junction: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateFamilyInvite(FamilyInvite invite)
        {
            try
            {
                _logger.LogInformation($"Updating invite for email: {invite.InvitedUserEmail} in family: {invite.FamilyId}");

                // Get the invite container PK property name
                string invitePkProperty = _invitePkPath.TrimStart('/');

                // Use the JSON manipulation approach for safe partition key handling
                var serializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore
                };

                var json = JsonConvert.SerializeObject(invite, serializerSettings);
                var inviteJObject = JObject.Parse(json);

                // Ensure the partition key property exists with the correct name
                string pkValue = invite.FamilyId;
                inviteJObject[invitePkProperty] = pkValue;

                // Save with the correct partition key
                await _invitesContainer.UpsertItemAsync<JObject>(
                    inviteJObject,
                    new PartitionKey(pkValue)
                );

                _logger.LogInformation($"Successfully updated invite for {invite.InvitedUserEmail}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating family invite: {ex.Message}");
                throw;
            }
        }

        #region Helper Methods

        private string GetPropertyValueCaseInsensitive<T>(T obj, string propertyName)
        {
            // Convert to JObject to get property regardless of case
            var json = JsonConvert.SerializeObject(obj);
            var jObj = JObject.Parse(json);

            foreach (var prop in jObj.Properties())
            {
                if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return prop.Value?.ToString();
                }
            }

            return null;
        }

        private async Task<T> ReadItemSafelyAsync<T>(Container container, string id, string partitionKeyValue, string partitionKeyPropertyName)
        {
            try
            {
                // First try with direct read
                var response = await container.ReadItemAsync<T>(
                    id,
                    new PartitionKey(partitionKeyValue)
                );

                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest &&
                                            ex.SubStatusCode == 1001)
            {
                // If we get a partition key mismatch, try reading it as JObject
                _logger.LogWarning($"Partition key mismatch when reading {typeof(T).Name}. Trying with JObject approach.");

                try
                {
                    // Create a query to get the item by ID
                    var query = new QueryDefinition($"SELECT * FROM c WHERE c.id = @id")
                        .WithParameter("@id", id);

                    using var iterator = container.GetItemQueryIterator<JObject>(query);

                    while (iterator.HasMoreResults)
                    {
                        var queryResponse = await iterator.ReadNextAsync();
                        if (queryResponse.Count > 0)
                        {
                            // Found the item, now get the correct partition key value
                            var jObj = queryResponse.First();
                            JToken pkToken = null;

                            // Try to find the partition key property
                            foreach (var prop in jObj.Properties())
                            {
                                if (string.Equals(prop.Name, partitionKeyPropertyName, StringComparison.OrdinalIgnoreCase))
                                {
                                    pkToken = prop.Value;
                                    break;
                                }
                            }

                            if (pkToken != null)
                            {
                                // Try to read with the correct partition key
                                var correctPkValue = pkToken.ToString();
                                var correctResponse = await container.ReadItemAsync<T>(
                                    id,
                                    new PartitionKey(correctPkValue)
                                );

                                return correctResponse.Resource;
                            }

                            // If we can't find the partition key property, try to deserialize the JObject
                            return jObj.ToObject<T>();
                        }
                    }
                }
                catch (Exception innerEx)
                {
                    _logger.LogError($"Error in fallback read: {innerEx.Message}");
                }

                // If we got here, we couldn't read the item
                throw;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning($"Item with ID {id} not found");
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error reading item with ID {id}: {ex.Message}");
                throw;
            }
        }

        #endregion
    }
}