using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Azure;
using Azure.AI.Vision.ImageAnalysis;
using Azure.Messaging.EventHubs;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using Grpc.Core;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Queues;
using OCR_AI_Grocey.Services.Interfaces;

namespace OCR_AI_Grocery
{
    public class ProcessReceiptOCR
    {
        private readonly ILogger<ProcessReceiptOCR> _logger;
        private readonly IReceiptProcessingService _receiptProcessingService;

        public ProcessReceiptOCR(
            ILogger<ProcessReceiptOCR> logger,
            IReceiptProcessingService receiptProcessingService)
        {
            _logger = logger;
            _receiptProcessingService = receiptProcessingService;
        }

        [Function("ProcessReceiptOCR")]
        [FixedDelayRetry(5, "00:00:10")]
        public async Task<string> EventHubFunction(
            [EventHubTrigger("reciept", Connection = "EventHubConnection")] string[] events,
            FunctionContext context)
        { 
            try
            {
                await _receiptProcessingService.ProcessReceiptEvents(events);
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in ProcessReceiptOCR: {ex.Message}");
                throw; // Rethrowing to trigger retry policy
            }
        }
    }
}
