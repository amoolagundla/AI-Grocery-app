using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCR_AI_Grocery.Models.Receipt;
using OCR_AI_Grocery.Models;
using OCR_AI_Grocey.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using System.Text.Json;
using OCR_AI_Grocey.Services.Helpers;

namespace OCR_AI_Grocey.Services.Implementations
{
    public class ReceiptProcessingService : IReceiptProcessingService
    {
        private readonly ILogger<ReceiptProcessingService> _logger;
        private readonly IReceiptService _receiptService;
        private readonly IBlobService _blobService;
        private readonly IOCRService _ocrService;
        private readonly IAnalysisQueue analysisQueue;

        public ReceiptProcessingService(
                ILogger<ReceiptProcessingService> logger,
                IReceiptService receiptService,
                IBlobService blobService,
                IOCRService ocrService,
                AnalysisSender serviceBusSender,
                IAnalysisQueue analysisQueue) // Change to inject the sender directly
        {
            _logger = logger;
            _receiptService = receiptService;
            _blobService = blobService;
            _ocrService = ocrService;
            this.analysisQueue = analysisQueue;
        }

        public async Task ProcessReceiptEvents(string[] events)
        {
            foreach (string eventJson in events)
            {
                await ProcessSingleEvent(eventJson);
            }
        }

        public async Task ProcessSingleEvent(string eventJson)
        {
            _logger.LogInformation($"Processing receipt event: {eventJson}");
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var eventData = System.Text.Json.JsonSerializer.Deserialize<List<EventGridEvent>>(eventJson, options)?.FirstOrDefault();

                if (eventData == null)
                {
                    _logger.LogWarning("Invalid event data. Skipping processing.");
                    return;
                }

                string blobUrl = eventData.Data.Url; // Now this will work correctly
                if (string.IsNullOrEmpty(blobUrl))
                {
                    _logger.LogWarning("Blob URL not found in event. Skipping processing.");
                    return;
                }

                var (Content, Metadata) = await _blobService.DownloadBlobWithMetadataAsync(blobUrl);
                if (Content == null)
                {
                    return;
                }

                string extractedText = await _ocrService.PerformOCR(Content);
                _logger.LogInformation($"Successfully extracted OCR Text: {extractedText}");

                await SaveReceiptToDatabase(eventData, extractedText, blobUrl, Metadata);
                await analysisQueue.SendToAnalysisQueue(Metadata, extractedText);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing receipt: {ex.Message}");
                throw; // Rethrowing to trigger retry policy
            }
        }

       

        private async Task SaveReceiptToDatabase(EventGridEvent eventData, string extractedText, string blobUrl, IDictionary<string, string> metadata)
        {
            var receipt = new ReceiptDocument
            {
                Id = Guid.NewGuid().ToString(),  // Ensure uniqueness
                UserId = metadata?.TryGetValue("email", out var userId) == true ? userId : "Unknown",
                FamilyId = metadata?.TryGetValue("familyId", out var familyId) == true ? familyId : "Unknown",
                ReceiptText = extractedText,
                BlobUrl = blobUrl,
                UploadDate = DateTime.UtcNow,
                StoreName = "Unknown", // Will be updated later
                PurchasedDate = DateTime.UtcNow // Placeholder until AI extracts the real date
            };


            await _receiptService.SaveReceiptAsync(receipt);
        }
    }
}
