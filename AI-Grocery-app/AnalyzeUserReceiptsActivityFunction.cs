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
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;
using static OCR_AI_Grocery.ProcessReceiptOCR;
using OCR_AI_Grocery.services;
using OCR_AI_Grocery.models;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using OCR_AI_Grocey.Services.Interfaces;

namespace OCR_AI_Grocery
{
    public class AnalyzeUserReceiptsActivityFunction
    {
        private readonly IAnalyzeUserReceiptsService _analyzeUserReceiptsService;
        private readonly ILogger<AnalyzeUserReceiptsActivityFunction> _logger;

        public AnalyzeUserReceiptsActivityFunction(
            IAnalyzeUserReceiptsService analyzeUserReceiptsService,
            ILoggerFactory loggerFactory)
        {
            _analyzeUserReceiptsService = analyzeUserReceiptsService;
            _logger = loggerFactory.CreateLogger<AnalyzeUserReceiptsActivityFunction>();
        }

        [Function("AnalyzeUserReceiptsActivityFunction")]
        public async Task Run(
            [ServiceBusTrigger("receipt-analysis-queue", Connection = "QueueConnectionString")]
        ServiceBusReceivedMessage message,
            FunctionContext context)
        {
            try
            {
                await _analyzeUserReceiptsService.ProcessReceiptAnalysis(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AnalyzeUserReceiptsActivityFunction");
                throw;
            }
        }
    }
}