using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.Notifications
{
    public class UserDeviceTokenService
    {
        private readonly Container _tokensContainer;
        private readonly ILogger<UserDeviceTokenService> _logger;

        public UserDeviceTokenService(CosmosClient cosmosClient, ILogger<UserDeviceTokenService> logger)
        {
            _logger = logger;
            _tokensContainer = cosmosClient.GetContainer("ReceiptsDB", "Tokens");
        }

        public async Task<List<string>> GetUserDeviceTokensAsync(string userEmail)
        {
            try
            {
                var query = new QueryDefinition(
                    "SELECT c.Token FROM c WHERE c.UserEmail = @userEmail")
                    .WithParameter("@userEmail", userEmail);

                var queryOptions = new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(userEmail)
                };

                using var iterator = _tokensContainer.GetItemQueryIterator<TokenResponse>(
                    query,
                    requestOptions: queryOptions
                );

                var tokens = new List<string>();
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    tokens.AddRange(response.Select(t => t.Token));
                }

                return tokens;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching device tokens for user {userEmail}");
                return new List<string>();
            }
        }
    }
     
}
