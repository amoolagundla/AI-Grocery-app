using Azure.Messaging.ServiceBus;
using FirebaseAdmin.Messaging;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCR_AI_Grocery.models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks; 
using Message = FirebaseAdmin.Messaging.Message;
using Notification = FirebaseAdmin.Messaging.Notification;

namespace OCR_AI_Grocery.Notifications
{
    public class SendPushNotificationFunction
    {
        private readonly FirebaseMessaging _firebaseMessaging;
        private readonly Container _usersContainer;
        private readonly ILogger _logger;
        private readonly CosmosClient _cosmosClient;

        public SendPushNotificationFunction(ILoggerFactory loggerFactory)
        {
            string cosmosDbConnection = Environment.GetEnvironmentVariable("CosmosDBConnectionString")?? string.Empty;
            _cosmosClient = new CosmosClient(cosmosDbConnection);
            _logger = loggerFactory.CreateLogger<SendPushNotificationFunction>();
            _usersContainer = _cosmosClient.GetContainer("ReceiptsDB", "users");
            _firebaseMessaging = FirebaseMessaging.DefaultInstance;
        }

        [Function("SendPushNotification")]
        public async Task Run(
            [ServiceBusTrigger("user-notifications-queue", Connection = "NotificaitonQueueConnectionString")] ServiceBusReceivedMessage message)
        {
            try
            {
                var notification = JsonConvert.DeserializeObject<NotificationMessage>(message.Body.ToString());
                var userTokens = await GetUserDeviceTokens(notification.UserId);

                if (!userTokens.Any())
                {
                    _logger.LogWarning($"No device tokens found for user {notification.UserId}");
                    return;
                }

                var fcmMessage = new MulticastMessage()
                {
                    Notification = new Notification
                    {
                        Title = notification.Title,
                        Body = notification.Body
                    },
                    Data = notification.Data,
                    Tokens = userTokens
                };

                var response = await _firebaseMessaging.SendEachForMulticastAsync(fcmMessage);
                _logger.LogInformation($"Sent notifications: {response.SuccessCount} successful, {response.FailureCount} failed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send push notification");
                throw;
            }
        }

        private async Task<List<string>> GetUserDeviceTokens(string userId)
        {
            var query = new QueryDefinition("SELECT c.deviceTokens FROM c WHERE c.id = @userId")
                .WithParameter("@userId", userId);

            using var iterator = _usersContainer.GetItemQueryIterator<DeviceTokenResponse>(query);
            var response = await iterator.ReadNextAsync();

            return response.FirstOrDefault()?.DeviceTokens ?? new List<string>();
        }

        private class DeviceTokenResponse
        {
            public List<string> DeviceTokens { get; set; }
        }
    }
}
