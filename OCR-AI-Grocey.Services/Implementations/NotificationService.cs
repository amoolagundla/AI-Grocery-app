using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCR_AI_Grocery.Models;
using OCR_AI_Grocey.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Implementations
{
    public class NotificationService : INotificationService
    {
        private readonly ServiceBusSender _notificationQueueSender;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            ServiceBusClient serviceBusClient,
            ILoggerFactory loggerFactory)
        {
            _notificationQueueSender = serviceBusClient.CreateSender("user-notifications-queue");
            _logger = loggerFactory.CreateLogger<NotificationService>();
        }

        public async Task SendNotification(
            ReceiptAnalysisMessage message,
            Dictionary<string, List<string>> newItems,
            ShoppingList shoppingList,
            string storeName)
        {
            var notification = new NotificationMessage
            {
                UserEmail = message.UserEmail,
                Title = storeName,
                Body = $"Your shopping list has been updated with {newItems.Sum(store => store.Value.Count)} new items from {newItems.Count} stores",
                Data = new Dictionary<string, string>
            {
                { "type", "shopping_list_update" },
                { "listId", shoppingList.Id }
            }
            };

            var serviceBusMessage = new ServiceBusMessage(JsonConvert.SerializeObject(notification));
            await _notificationQueueSender.SendMessageAsync(serviceBusMessage);
            _logger.LogInformation($"Sent notification to {message.UserEmail}");
        }
    }
}
