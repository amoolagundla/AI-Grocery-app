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
        private readonly FirebaseMessagingService _firebaseService;
        private readonly UserDeviceTokenService _tokenService;
        private readonly ILogger<SendPushNotificationFunction> _logger;

        public SendPushNotificationFunction(
            FirebaseMessagingService firebaseService,
            UserDeviceTokenService tokenService,
            ILoggerFactory loggerFactory)
        {
            _firebaseService = firebaseService;
            _tokenService = tokenService;
            _logger = loggerFactory.CreateLogger<SendPushNotificationFunction>();
        }

        [Function("SendPushNotification")]
        public async Task Run(
            [ServiceBusTrigger("user-notifications-queue", Connection = "NotificaitonQueueConnectionString")] ServiceBusReceivedMessage message)
        {
            try
            {
                var notification = System.Text.Json.JsonSerializer.Deserialize<NotificationMessage>(message.Body.ToString());
                var userTokens = await _tokenService.GetUserDeviceTokensAsync(notification.UserEmail);

                if (!userTokens.Any())
                {
                    _logger.LogWarning($"No device tokens found for user {notification.UserEmail}");
                    return;
                }

                int successCount = 0, failureCount = 0;

                foreach (var token in userTokens)
                {
                    bool success = await _firebaseService.SendNotificationAsync(
                        token,
                        notification.Title,
                        notification.Body,
                        notification.Data
                    );

                    if (success) successCount++;
                    else failureCount++;
                }

                _logger.LogInformation($"Sent notifications: {successCount} successful, {failureCount} failed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process push notification");
                throw;
            }
        }
    } 
}
