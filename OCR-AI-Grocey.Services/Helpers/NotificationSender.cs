using Azure.Messaging.ServiceBus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Helpers
{

    public class NotificationClient : ServiceBusClient
    {
        public NotificationClient(string connectionString) : base(connectionString) { }
    }

    public class AnalysisSender
    {
        private readonly ServiceBusSender _sender;

        public AnalysisSender(ServiceBusSender sender)
        {
            _sender = sender;
        }

        public Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            return _sender.SendMessageAsync(message, cancellationToken);
        }
    }

    public class NotificationSender
    {
        private readonly ServiceBusSender _sender;

        public NotificationSender(ServiceBusSender sender)
        {
            _sender = sender;
        }

        public Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            return _sender.SendMessageAsync(message, cancellationToken);
        }
    }

    public class AIMLSender
    {
        private readonly ServiceBusSender _sender;

        public AIMLSender(ServiceBusSender sender)
        {
            _sender = sender;
        }

        public Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            return _sender.SendMessageAsync(message, cancellationToken);
        }
    }
}
