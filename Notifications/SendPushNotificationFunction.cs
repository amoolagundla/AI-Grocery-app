using Azure.Messaging.ServiceBus;
using Google.Apis.Auth.OAuth2;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.Notifications
{
    public class SendPushNotificationFunction
    {
        private readonly Container _tokensContainer;
        private readonly ILogger _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly HttpClient _httpClient;
        private readonly string _firebaseProjectId;
        private readonly string _serviceAccountJson;
        private GoogleCredential _credential;

        public SendPushNotificationFunction(ILoggerFactory loggerFactory)
        {
            string cosmosDbConnection = Environment.GetEnvironmentVariable("CosmosDBConnectionString") ?? string.Empty;

            // Read the service account JSON from file
            string configPath = Path.Combine(AppContext.BaseDirectory, "service-account.json");
            string serviceAccountJson = File.ReadAllText(configPath);

            _cosmosClient = new CosmosClient(cosmosDbConnection);
            _logger = loggerFactory.CreateLogger<SendPushNotificationFunction>();
            _tokensContainer = _cosmosClient.GetContainer("ReceiptsDB", "Tokens");
            _httpClient = new HttpClient();

            // Parse config and get project ID
            var config = JsonDocument.Parse(serviceAccountJson);
            _firebaseProjectId = config.RootElement.GetProperty("project_id").GetString() ?? string.Empty;

            // Initialize Google credential
            _credential = GoogleCredential.FromJson(serviceAccountJson)
                .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
        }

        private async Task<string> GetAccessTokenAsync()
        {
            var token = await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync();
            return token;
        }

        [Function("SendPushNotification")]
        public async Task Run(
            [ServiceBusTrigger("user-notifications-queue", Connection = "NotificaitonQueueConnectionString")] ServiceBusReceivedMessage message)
        {
            try
            {
                // Get fresh access token
                var accessToken = await GetAccessTokenAsync();
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                var notification = JsonConvert.DeserializeObject<NotificationMessage>(message.Body.ToString());
                var userTokens = await GetUserDeviceTokens(notification.UserEmail);

                if (!userTokens.Any())
                {
                    _logger.LogWarning($"No device tokens found for user {notification.UserEmail}");
                    return;
                }

                int successCount = 0;
                int failureCount = 0;

                foreach (var token in userTokens)
                {
                    try
                    {
                        var fcmMessage = new
                        {
                            message = new
                            {
                                token = token,
                                notification = new
                                {
                                    title = notification.Title,
                                    body = notification.Body
                                },
                                data = notification.Data
                            }
                        };

                        var json = JsonConvert.SerializeObject(fcmMessage);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                        var fcmUrl = $"https://fcm.googleapis.com/v1/projects/{_firebaseProjectId}/messages:send";
                        var response = await _httpClient.PostAsync(fcmUrl, content);

                        if (response.IsSuccessStatusCode)
                        {
                            successCount++;
                        }
                        else
                        {
                            failureCount++;
                            var errorResponse = await response.Content.ReadAsStringAsync();
                            _logger.LogError($"Failed to send notification to token {token}. Error: {errorResponse}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        _logger.LogError(ex, $"Error sending notification to token {token}");
                    }
                }

                _logger.LogInformation($"Sent notifications: {successCount} successful, {failureCount} failed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process push notification");
                throw;
            }
        }

        private async Task<List<string>> GetUserDeviceTokens(string userEmail)
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