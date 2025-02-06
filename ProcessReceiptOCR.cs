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

namespace OCR_AI_Grocery
{
    public class ProcessReceiptOCR
    {
        private readonly ILogger<ProcessReceiptOCR> _logger;
        private const string StorageAccountName = "reciepts"; // Corrected spelling
        private const string ContainerName = "receipts";

        // Azure AI Vision Credentials
        private const string VisionEndpoint = "https://reciept-vision.cognitiveservices.azure.com/";
        private const string VisionKey = "EzK1s1e1KwCa3ecEzzG8MnWk7caCsbd698URjSn9NltjqIOkfRQQJQQJ99BBACYeBjFXJ3w3AAAFACOGzB6B";

        public ProcessReceiptOCR(ILogger<ProcessReceiptOCR> logger)
        {
            _logger = logger;
        }
        
        [Function("ProcessReceiptOCR")] 
        [FixedDelayRetry(5, "00:00:10")]
        [EventHubOutput("dest", Connection = "EventHubConnection")]
        public async  Task<string> EventHubFunction(
        [EventHubTrigger("reciept", Connection = "EventHubConnection")] string[] input,
        FunctionContext context,ILogger log)
        {
            foreach(string eventBody in input)
            {
                log.LogInformation($"ProcessReceiptOCR function processing event: {eventBody}");
                try
                {
                    string blobUrl = ExtractBlobUrl(eventBody);
                    if (string.IsNullOrEmpty(blobUrl))
                    {
                        log.LogWarning("Blob URL not found in event. Skipping processing.");
                         
                    }

                    using var blobContent = await DownloadBlobAsync(blobUrl);
                    if (blobContent == null)
                    {
                        log.LogError($"Failed to download blob from {blobUrl}. Skipping processing.");
                        
                    }

                    string extractedText = await PerformOCR(blobContent);
                    log.LogInformation($"Successfully extracted OCR Text: {extractedText}");

                    // TODO: Implement saving extracted text to Azure Data Lake or CosmosDB
                    // await SaveExtractedTextAsync(extractedText);
                }
                catch (Exception ex)
                {
                    log.LogError($"Error in ProcessReceiptOCR: {ex.Message}");
                    // Consider adding telemetry or alerting here for critical errors
                }
            }
            

            return string.Empty;
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

        private async Task<Stream> DownloadBlobAsync(string blobUrl)
        {
            try
            {
                var blobClient = new BlobClient(new Uri(blobUrl), new Azure.Identity.DefaultAzureCredential());
                var response = await blobClient.DownloadStreamingAsync();
                return response.Value.Content;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error downloading blob: {ex.Message}");
                return null;
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
