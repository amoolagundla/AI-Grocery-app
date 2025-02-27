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
using ReceiptDocument = OCR_AI_Grocery.ProcessReceiptOCR.ReceiptDocument;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;

namespace OCR_AI_Grocery
{
    public class AnalyzeUserReceiptsActivityFunction
    {
        private readonly Container _receiptsContainer;
        private readonly Container _shoppingListContainer;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger; 
        private readonly ServiceBusClient _serviceBusClient;
        private CleanJsonResponseHelper cleanJsonResponseHelper;
        private readonly ServiceBusSender _notificationQueueSender;

        public AnalyzeUserReceiptsActivityFunction(ILoggerFactory loggerFactory, HttpClient httpClient)
        {

            string cosmosDbConnection = Environment.GetEnvironmentVariable("CosmosDBConnectionString") ?? string.Empty;
            _cosmosClient = new CosmosClient(cosmosDbConnection);
            _logger = loggerFactory.CreateLogger<AnalyzeUserReceiptsActivityFunction>();
            _receiptsContainer = _cosmosClient.GetContainer("ReceiptsDB", "receipts");
            _shoppingListContainer = _cosmosClient.GetContainer("ReceiptsDB", "ShoppingLists");
            _container = _cosmosClient.GetContainer("ReceiptsDB", "receipts"); 
            cleanJsonResponseHelper = new CleanJsonResponseHelper(loggerFactory); 
            string serviceBusConnectionString = Environment.GetEnvironmentVariable("NotificaitonQueueConnectionString")
             ?? throw new InvalidOperationException("Service Bus connection string not found");

            // Create Service Bus client and sender
            _serviceBusClient = new ServiceBusClient(serviceBusConnectionString); 
            _notificationQueueSender = _serviceBusClient.CreateSender("user-notifications-queue");
        }

        [Function("AnalyzeUserReceiptsActivityFunction")]
        public async Task Run([ServiceBusTrigger("receipt-analysis-queue", Connection = "QueueConnectionString")] ServiceBusReceivedMessage message, FunctionContext context)
        {
            try
            {
                // Main processing
                var receiptMessage = JsonConvert.DeserializeObject<ReceiptAnalysisMessage>(message.Body.ToString());

                if (string.IsNullOrEmpty(receiptMessage?.FamilyId))
                {
                    throw new ArgumentException("Missing UserEmail in queue message");
                }

                var receipts = await FetchReceipts(receiptMessage.FamilyId);
                if (!receipts.Any())
                {
                    throw new InvalidOperationException($"No receipts found for {receiptMessage.FamilyId}");
                }

                var groupedItems = await AnalyzeReceiptsWithOpenAI(receipts);
                if (groupedItems == null || !groupedItems.Any())
                {
                    throw new InvalidOperationException("No shopping list data extracted");
                }

                var shoppingList = new ShoppingList
                {
                    Id = receiptMessage.FamilyId,
                    UserId = receiptMessage.UserEmail,
                    StoreItems = groupedItems,
                    CreatedAt = DateTime.UtcNow
                };

                // Use transactions or batch operations if possible
                await _shoppingListContainer.UpsertItemAsync(shoppingList, new PartitionKey(shoppingList.UserId));
                var storeName= await UpdateReceipts(receipts, groupedItems);

                // Message is auto-completed only if no exceptions are thrown

                // After successful processing:
                await SendNotification(receiptMessage, groupedItems, shoppingList, storeName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing receipts");
                // Message will be abandoned and retried
                throw;
            }
        }

        private async Task SendNotification(ReceiptAnalysisMessage? receiptMessage, Dictionary<string, List<string>> groupedItems, ShoppingList shoppingList,string storeName)
        {
            var notification = new NotificationMessage
            {
                UserEmail = receiptMessage.UserEmail,
                Title = storeName,
                Body = $"Your shopping list has been updated with items from {groupedItems.Count} stores",
                Data = new Dictionary<string, string>
                            {
                                { "type", "shopping_list_update" },
                                { "listId", shoppingList.Id }
                            }
                                    };

            await _notificationQueueSender.SendMessageAsync(
                new ServiceBusMessage(JsonConvert.SerializeObject(notification))
            );
        }

        private async Task<string> UpdateReceipts(List<ReceiptDocument> receipts, Dictionary<string, List<string>> groupedItems)
        {
            var returnString = string.Empty;
            foreach (var store in groupedItems)
            {
                string extractedStoreName = NormalizeStoreName(store.Key);
                returnString = extractedStoreName;
                foreach (var item in store.Value)
                {
                    var receiptToUpdate = receipts.FirstOrDefault(r => r.ReceiptText.Contains(item, StringComparison.OrdinalIgnoreCase));

                    if (receiptToUpdate != null)
                    {
                        // Extract Store Name from Receipt Text 
                        if (!string.IsNullOrEmpty(extractedStoreName))
                        {
                            extractedStoreName = extractedStoreName;  // Use extracted name if found
                        }

                        receiptToUpdate.StoreName = extractedStoreName;
                        receiptToUpdate.PurchasedDate = DateTime.UtcNow; // Adjust if AI extracts actual date

                        await _receiptsContainer.ReplaceItemAsync(receiptToUpdate, receiptToUpdate.Id, new PartitionKey(receiptToUpdate.FamilyId));
                        _logger.LogInformation($"✅ Updated receipt {receiptToUpdate.Id} with Store: {extractedStoreName}");
                    }
                }
            }
            return returnString;
        }
        private string ExtractStoreName(string receiptText)
        {
            // Common store name patterns
            string storePattern = @"^(.*?)(?:\d{3,}|\bServer\b|\bCheck\b|Ordered:|\$|Subtotal|Tax|Total)";

            var match = Regex.Match(receiptText, storePattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string storeName = match.Groups[1].Value.Trim();
                return NormalizeStoreName(storeName);
            }

            return string.Empty;
        }
        private string NormalizeStoreName(string storeName)
        {
            if (string.IsNullOrEmpty(storeName))
                return "Unknown Store";

            // Remove special characters except spaces
            storeName = Regex.Replace(storeName, @"[^a-zA-Z0-9\s]", "");

            // Trim spaces and capitalize first letter
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(storeName.ToLower().Trim());
        }


        private async Task<List<ReceiptDocument>> FetchReceipts(string userEmail)
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.FamilyId = @userEmail")
                    .WithParameter("@userEmail", userEmail);

            var receipts = new List<ReceiptDocument>();

            using FeedIterator<ReceiptDocument> queryIterator = _receiptsContainer.GetItemQueryIterator<ReceiptDocument>(query);
            while (queryIterator.HasMoreResults)
            {
                FeedResponse<ReceiptDocument> response = await queryIterator.ReadNextAsync();
                receipts.AddRange(response);
            }

            if (receipts.Count == 0)
            {
                _logger.LogWarning("⚠️ No receipts found for {UserEmail}", userEmail);
                return new List<ReceiptDocument>();
            }

            return receipts;
        }

        private async Task<Dictionary<string, List<string>>> AnalyzeReceiptsWithOpenAI(List<ReceiptDocument> receipts)
        {
            var allReceiptsText = string.Join("\n\n", receipts.Select(r => r.ReceiptText));

            var prompt = $@"
        Analyze these receipts and create a structured shopping list. Follow these rules exactly:

        1. Format store names:
           - Replace apostrophes with 's' (Braum's → Braums)
           - Remove special characters
           - Example: 'Sam's Club' → 'Sams Club'

        2. Format item names:
           - Expand abbreviations
           - Use common names
           - Examples:
             'CASCPLATPLUS' → 'Cascade Platinum Plus -- with company name if available'
             'OJ' → 'Orange Juice  -- with company name if available'

        3. Return ONLY valid JSON in this exact format:
        {{
            ""Walmart"": [
                ""Milk"",
                ""Bread"",
                ""Eggs""
            ],
            ""Costco Wholesale"": [
                ""Paper Towels"",
                ""Orange Juice"",
                ""Chicken Breast""
            ]
        }}

        Do not include:
        - Prices
        - Quantities
        - Extra text or explanations
        - Markdown formatting

        Receipts to analyze:
        {allReceiptsText}";

            var requestBody = new
            {
                model = "gpt-4o-mini",
                messages = new[]
                {
            new { role = "system", content = "You are a precise JSON formatter. Return only valid JSON without any additional text or explanations." },
            new { role = "user", content = prompt }
        },
                max_tokens = 500,
                temperature = 0.3  // Lower temperature for more consistent formatting
            };

            var jsonRequest = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            string openAiApiKey = Environment.GetEnvironmentVariable("OpenAI_API_Key")
                ?? throw new InvalidOperationException("OpenAI API Key not found");

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", openAiApiKey);

            var response = await httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"❌ OpenAI API call failed: {responseString}");
                return new Dictionary<string, List<string>>();
            }

            var openAIResponse = JsonConvert.DeserializeObject<OpenAIResponse>(responseString);
            if (openAIResponse?.Choices == null || !openAIResponse.Choices.Any())
            {
                _logger.LogError("❌ No choices found in OpenAI response");
                return new Dictionary<string, List<string>>();
            }

            var aiGeneratedText = openAIResponse.Choices[0].Message.Content;
            _logger.LogInformation($"🤖 AI Generated Text: {aiGeneratedText}");

            try
            {
                // Should now be clean JSON without need for additional cleaning
                var result = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(aiGeneratedText);
                return result ?? new Dictionary<string, List<string>>();
            }
            catch (JsonException ex)
            {
                _logger.LogError($"❌ JSON parsing error: {ex.Message}");
                return new Dictionary<string, List<string>>();
            }
        }

        public class OpenAIResponse
        {
            public class Choice
            {
                [JsonProperty("message")]
                public Message Message { get; set; } = new Message();
            }

            public class Message
            {
                [JsonProperty("content")]
                public string Content { get; set; } = string.Empty;
            }

            [JsonProperty("choices")]
            public List<Choice> Choices { get; set; } = new List<Choice>();
        }

    }

    // 📨 Service Bus Message Model
    public class ReceiptAnalysisMessage
    {
        [JsonProperty("familyId")]
        public string FamilyId { get; set; }

        [JsonProperty("userEmail")]
        public string UserEmail { get; set; }
    }

    // 🛒 Cosmos DB Model for Shopping Lists
    public class ShoppingList
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public string UserId { get; set; }
        public Dictionary<string, List<string>> StoreItems { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // 🧾 Processed Receipts Model
    public class ProcessedReceipt
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public string UserId { get; set; }
        public string ReceiptText { get; set; }
        public string StoreName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime PurchasedDate { get; set; }
    }
}
