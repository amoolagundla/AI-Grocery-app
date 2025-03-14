using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;
using OCR_AI_Grocey.Services.Helpers;
using OCR_AI_Grocey.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Implementations
{
    public class AnalysisQueue : IAnalysisQueue
    {
        private readonly AnalysisSender _queueSender;

        public AnalysisQueue(AnalysisSender queueSender)
        {
            _queueSender = queueSender;
        }

        public   async Task SendToAnalysisQueue(IDictionary<string, string> metadata, string extractedText)
        {
            var queueMessage = JsonConvert.SerializeObject(new
            {

                UserEmail = metadata?.TryGetValue("email", out var userId) == true ? userId : "Unknown",
                FamilyId = metadata?.TryGetValue("familyId", out var familyId) == true ? familyId : "Unknown"
            });

            var message = new ServiceBusMessage(Encoding.UTF8.GetBytes(queueMessage))
            {
                ContentType = "application/json",
                Subject = "ReceiptAnalysis",
                MessageId = Guid.NewGuid().ToString()
            };

            await _queueSender.SendMessageAsync(message);
        }
    }
}
