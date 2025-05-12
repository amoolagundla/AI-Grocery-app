using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCR_AI_Grocery.Models;
using OCR_AI_Grocey.Services.Helpers;
using OCR_AI_Grocey.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Implementations
{
    public class AIMLInterface : IAIMLInterface
    {
        private readonly ILogger<AIMLInterface> _logger;
        private readonly AIMLSender _sender;

        public AIMLInterface(ILogger<AIMLInterface> logger, AIMLSender sender)
        {
            _logger = logger;
            _sender = sender;
        }

        public async Task SendNotification(string message)
        { 
            var serviceBusMessage = new ServiceBusMessage(message);
            await _sender.SendMessageAsync(serviceBusMessage);
            _logger.LogInformation($"Sent to queue to AIML queue");
        }
    }
}
