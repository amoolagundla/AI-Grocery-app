using Azure.Messaging.ServiceBus;
using OCR_AI_Grocey.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Implementations
{
    public class NotificationServiceBusClientAdapter : INotificationServiceBusClient
    {
        private readonly ServiceBusClient _client;

        public NotificationServiceBusClientAdapter(ServiceBusClient client)
        {
            _client = client;
        }

        public ServiceBusSender CreateSender(string queueOrTopicName)
        {
            return _client.CreateSender(queueOrTopicName);
        }

        public ValueTask DisposeAsync()
        {
            return _client.DisposeAsync();
        }
    }
}
