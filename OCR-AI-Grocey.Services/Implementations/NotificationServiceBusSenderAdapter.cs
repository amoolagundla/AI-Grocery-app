using Azure.Messaging.ServiceBus;
using OCR_AI_Grocey.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Implementations
{
    public class NotificationServiceBusSenderAdapter : INotificationServiceBusSender
    {
        private readonly ServiceBusSender _sender;

        public NotificationServiceBusSenderAdapter(ServiceBusSender sender)
        {
            _sender = sender;
        }

        public Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            return _sender.SendMessageAsync(message, cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return _sender.DisposeAsync();
        }
    }
}
