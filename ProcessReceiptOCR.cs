﻿using System;
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

namespace OCR_AI_Grocery
{
    public class ProcessReceiptOCR
    {
        private readonly ILogger _logger;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;

        private   QueueClient _queueClient;
        private const string StorageAccountName = "reciepts"; // Corrected spelling
        private const string ContainerName = "receipts";

        // Azure AI Vision Credentials
        private const string VisionEndpoint = "https://reciept-vision.cognitiveservices.azure.com/";
        private const string VisionKey = "EzK1s1e1KwCa3ecEzzG8MnWk7caCsbd698URjSn9NltjqIOkfRQQJQQJ99BBACYeBjFXJ3w3AAAFACOGzB6B";
        private readonly ServiceBusSender _queueSender;
        private readonly ServiceBusClient _serviceBusClient;

        public ProcessReceiptOCR(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ProcessReceiptOCR>(); 
            string cosmosDbConnection = Environment.GetEnvironmentVariable("CosmosDBConnectionString") ?? string.Empty;
            _cosmosClient = new CosmosClient(cosmosDbConnection);
            _container = _cosmosClient.GetContainer("ReceiptsDB", "receipts");
            string serviceBusConnectionString = Environment.GetEnvironmentVariable("QueueConnectionString")
                ?? throw new InvalidOperationException("Service Bus connection string not found");

            // Create Service Bus client and sender
            _serviceBusClient = new ServiceBusClient(serviceBusConnectionString);
            _queueSender = _serviceBusClient.CreateSender("receipt-analysis-queue");
 
        }

        [Function("ProcessReceiptOCR")]
        [FixedDelayRetry(5, "00:00:10")]
        public async Task<string> EventHubFunction(
    [EventHubTrigger("reciept", Connection = "EventHubConnection")] string[] events,
    FunctionContext context)
        {
            var log = context.GetLogger("ProcessReceiptOCR");

            foreach (string eventJson in events)
            {
                log.LogInformation($"ProcessReceiptOCR function processing event: {eventJson}");
                try
                {
                    var eventData = System.Text.Json.JsonSerializer.Deserialize<List<EventGridEvent>>(eventJson)?.FirstOrDefault();
                    if (eventData == null)
                    {
                        log.LogWarning("Invalid event data. Skipping processing.");
                        continue;
                    }

                    string blobUrl = eventData.data.url;
                    if (string.IsNullOrEmpty(blobUrl))
                    {
                        log.LogWarning("Blob URL not found in event. Skipping processing.");
                        continue;
                    }

                    (Stream Content, IDictionary<string, string> Metadata) = await DownloadBlobWithMetadataAsync(blobUrl);

                    if (Content != null)
                    {
                        foreach (var tag in Metadata)
                        {
                            Console.WriteLine($"Tag Key: {tag.Key}, Value: {tag.Value}");
                        }
                    }

                    string extractedText = await PerformOCR(Content);
                    log.LogInformation($"Successfully extracted OCR Text: {extractedText}"); 
                    await SaveToCosmosDb(eventData, extractedText, blobUrl, Metadata);
                    await NewMethod(eventData, blobUrl, Metadata, extractedText);

                    log.LogInformation($"Successfully SendMessageAsync to queue  ");
                }
                catch (Exception ex)
                {
                    log.LogError($"Error in ProcessReceiptOCR: {ex.Message}");
                    // Consider adding telemetry or alerting here for critical errors
                }
            }

            return string.Empty;
        }

        private async Task NewMethod(EventGridEvent? eventData, string blobUrl, IDictionary<string, string> Metadata, string extractedText)
        {

            var queueMessage = JsonConvert.SerializeObject(new { userEmail = Metadata?.TryGetValue("email", out var userId) == true ? userId : "Unknown", familyId = Metadata?.TryGetValue("familyId", out var familyId) == true ? familyId : "Unknown" });
            var message = new ServiceBusMessage(Encoding.UTF8.GetBytes(queueMessage))
            {
                ContentType = "application/json",
                Subject = "ReceiptAnalysis",
                MessageId = Guid.NewGuid().ToString()
            };

            // Send message to Service Bus queue
            await _queueSender.SendMessageAsync(message);
        }

        private async Task SaveToCosmosDb(EventGridEvent eventData, string extractedText,string bloblurl, IDictionary<string, string> metadata)
        {
            var receipt = new ReceiptDocument
            {
                Id = Guid.NewGuid().ToString(),  // Ensure uniqueness
                UserId = metadata?.TryGetValue("email", out var userId) == true ? userId : "Unknown",
                FamilyId = metadata?.TryGetValue("familyId", out var familyId) == true ? familyId : "Unknown",
                ReceiptText = extractedText,
                BlobUrl = bloblurl,
                UploadDate = DateTime.UtcNow,
                StoreName = "Unknown", // Will be updated later
                PurchasedDate = DateTime.UtcNow // Placeholder until AI extracts the real date
            };

            await _container.CreateItemAsync(receipt, new PartitionKey(receipt.FamilyId));
            _logger.LogInformation($"Saved receipt to Cosmos DB for user: {receipt.UserId}, family: {receipt.FamilyId}");
        }

        // Receipt Document Model for Cosmos DB
        public class ReceiptDocument
        {
            [JsonProperty("id")]
            public string Id { get; set; } = Guid.NewGuid().ToString();  // CosmosDB requires an "id" field

            [JsonProperty("FamilyId")]
            public string FamilyId { get; set; }  // ✅ Partition Key (MUST MATCH)

            [JsonProperty("UserId")]
            public string UserId { get; set; }

            [JsonProperty("ReceiptText")]
            public string ReceiptText { get; set; }

            [JsonProperty("StoreName")]
            public string StoreName { get; set; }

            [JsonProperty("UploadDate")]
            public DateTime UploadDate { get; set; } = DateTime.UtcNow;

            [JsonProperty("BlobUrl")]
            public string BlobUrl { get; set; }
            public DateTime PurchasedDate { get; internal set; }
        }

        public class EventGridEvent
        {
            public string topic { get; set; }
            public string subject { get; set; }
            public string eventType { get; set; }
            public string id { get; set; }
            public Data data { get; set; }
            public string dataVersion { get; set; }
            public string metadataVersion { get; set; }
            public DateTime eventTime { get; set; }
        }

        public class Data
        {
            public string api { get; set; }
            public string clientRequestId { get; set; }
            public string requestId { get; set; }
            public string eTag { get; set; }
            public string contentType { get; set; }
            public int contentLength { get; set; }
            public string blobType { get; set; }
            public string accessTier { get; set; }
            public string url { get; set; }
            public string sequencer { get; set; }
            public Storagediagnostics storageDiagnostics { get; set; }
        }

        public class Storagediagnostics
        {
            public string batchId { get; set; }
        }


        private string ExtractBlobUrl(string eventBody)
        {
            try
            {
                var json = System.Text.Json.JsonDocument.Parse(eventBody);
                return json.RootElement.GetProperty("data").GetProperty("url").GetString();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task<(Stream Content, IDictionary<string, string> Metadata)> DownloadBlobWithMetadataAsync(string blobUrl)
        {
            try
            {
                var blobClient = new BlobClient(new Uri(blobUrl), new DefaultAzureCredential());

                // Download the blob content
                var response = await blobClient.DownloadStreamingAsync();
                Stream blobContent = response.Value.Content;

                // Retrieve Blob Metadata
                var propertiesResponse = await blobClient.GetPropertiesAsync();
                var metadata = propertiesResponse.Value.Metadata;

                _logger.LogInformation($"✅ Successfully downloaded blob with {metadata.Count} metadata entries.");

                return (blobContent, metadata);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error downloading blob: {ex.Message}");
                return (null, null);
            }
        }



        private async Task<string> PerformOCR(Stream imageStream)
        {
            try
            {
                var credential = new AzureKeyCredential(VisionKey);
                var client = new ImageAnalysisClient(new Uri(VisionEndpoint), credential);
                var result = await client.AnalyzeAsync(BinaryData.FromStream(imageStream), VisualFeatures.Read);

                if (result.Value != null)
                {
                    return string.Join(" ", result.Value.Read.Blocks.SelectMany(b => b.Lines).Select(l => l.Text));
                }
                return "No text found.";
            }
            catch (Exception ex)
            {
                _logger.LogError($"OCR Error: {ex.Message}");
                return "OCR Failed.";
            }
        }
    }
}
