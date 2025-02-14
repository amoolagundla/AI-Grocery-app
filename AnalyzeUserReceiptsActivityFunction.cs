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
        private readonly ServiceBusSender _queueSender;
        private readonly ServiceBusClient _serviceBusClient;

        public AnalyzeUserReceiptsActivityFunction(ILoggerFactory loggerFactory, HttpClient httpClient)
        {

            string cosmosDbConnection = Environment.GetEnvironmentVariable("CosmosDBConnectionString") ?? string.Empty;
            _cosmosClient = new CosmosClient(cosmosDbConnection);
            _logger = loggerFactory.CreateLogger<AnalyzeUserReceiptsActivityFunction>(); 
            _receiptsContainer = _cosmosClient.GetContainer("ReceiptsDB", "receipts");
            _shoppingListContainer = _cosmosClient.GetContainer("ReceiptsDB", "ShoppingLists");
            string serviceBusConnectionString = Environment.GetEnvironmentVariable("QueueConnectionString")
                ?? throw new InvalidOperationException("Service Bus connection string not found");

            // Create Service Bus client and sender
            _serviceBusClient = new ServiceBusClient(serviceBusConnectionString);
            _queueSender = _serviceBusClient.CreateSender("receipt-analysis-queue");
          _httpClient = httpClient;
        }

        [Function("AnalyzeUserReceiptsActivityFunction")]
        public async Task Run(
            [ServiceBusTrigger("receipt-analysis-queue", Connection = "QueueConnectionString")] ServiceBusReceivedMessage message,
            FunctionContext context)
        {
           

            try
            {
                _logger.LogInformation("📩 Received Message ID: {MessageId}", message.MessageId);
                _logger.LogInformation("📄 Message Body: {Body}", message.Body.ToString());

                var receiptMessage = JsonConvert.DeserializeObject<ReceiptAnalysisMessage>(message.Body.ToString());
                string userEmail = receiptMessage?.UserEmail ?? string.Empty;

                if (string.IsNullOrEmpty(userEmail))
                {
                    _logger.LogError("🚨 Invalid queue message: UserEmail is missing.");
                    return;
                }

                _logger.LogInformation("🔍 Analyzing receipts for user: {UserEmail}", userEmail);

                // Fetch user's receipts from Cosmos DB
                var query = new QueryDefinition("SELECT * FROM c WHERE c.UserId = @userEmail")
                    .WithParameter("@userEmail", userEmail);

                var receipts = new List<ProcessedReceipt>();

                using FeedIterator<ProcessedReceipt> queryIterator = _receiptsContainer.GetItemQueryIterator<ProcessedReceipt>(query);
                while (queryIterator.HasMoreResults)
                {
                    FeedResponse<ProcessedReceipt> response = await queryIterator.ReadNextAsync();
                    receipts.AddRange(response);
                }

                if (receipts.Count == 0)
                {
                    _logger.LogWarning("⚠️ No receipts found for {UserEmail}", userEmail);
                    return;
                }

                // 🔄 Group receipts by store name using OpenAI
                var groupedItems = await AnalyzeReceiptsWithOpenAI(receipts);

                if (groupedItems == null || groupedItems.Count == 0)
                {
                    _logger.LogWarning("⚠️ No relevant shopping list data extracted for {UserEmail}", userEmail);
                    return;
                }

                // 🛒 Store analysis results in CosmosDB
                var shoppingList = new ShoppingList
                {
                    Id = userEmail,
                    UserId = userEmail,
                    StoreItems = groupedItems,
                    CreatedAt = DateTime.UtcNow
                };

                await _shoppingListContainer.UpsertItemAsync(shoppingList, new PartitionKey(shoppingList.UserId));

                _logger.LogInformation("✅ Shopping list stored successfully for {UserEmail}", userEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in AnalyzeReceipt function");
            }
        }

        private async Task<Dictionary<string, List<string>>> AnalyzeReceiptsWithOpenAI(List<ProcessedReceipt> receipts)
        {
            var allReceiptsText = string.Join("\n\n", receipts.Select(r => r.ReceiptText));
            var prompt = $"Analyze the following receipts and group the frequently bought items by store name:\n\n{allReceiptsText}\n\n" +
                        "Return only valid JSON in the following format without any additional text:\n" +
                        "{ \"store1\": [\"item1\", \"item2\"], \"store2\": [\"item3\", \"item4\"] }";

            var requestBody = new
            {
                model = "gpt-4",
                messages = new[]
                {
                new { role = "system", content = "You are a helpful AI assistant that analyzes shopping receipts. Always return valid JSON." },
                new { role = "user", content = prompt }
            },
                max_tokens = 500,
                temperature = 0.6
            };

            var jsonRequest = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            // Get OpenAI API Key
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

            // Parse the OpenAI response
            var openAIResponse = JsonConvert.DeserializeObject<OpenAIResponse>(responseString);
            if (openAIResponse?.Choices == null || !openAIResponse.Choices.Any())
            {
                _logger.LogError("❌ No choices found in OpenAI response");
                return new Dictionary<string, List<string>>();
            }

            var aiGeneratedText = openAIResponse.Choices[0].Message.Content;
            _logger.LogInformation($"AI Generated Text: {aiGeneratedText}");

            // Try to clean and parse the JSON response
            aiGeneratedText = CleanJsonResponse(aiGeneratedText);

            try
            {
                var result = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(aiGeneratedText);
                if (result == null)
                {
                    _logger.LogError("❌ Failed to deserialize AI response to dictionary");
                    return new Dictionary<string, List<string>>();
                }
                return result;
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

        private string CleanJsonResponse(string response)
        {
            try
            {
                // Remove any text before the first {
                var startIndex = response.IndexOf('{');
                var endIndex = response.LastIndexOf('}');

                if (startIndex == -1 || endIndex == -1)
                {
                    _logger.LogWarning("❌ Could not find valid JSON markers in response");
                    return "{}";
                }

                // Extract just the JSON part
                response = response.Substring(startIndex, endIndex - startIndex + 1);

                // Replace single quotes with double quotes if present
                response = response.Replace('\'', '"');

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error cleaning JSON response: {ex.Message}");
                return "{}";
            }
        }
    }

    // 📨 Service Bus Message Model
    public class ReceiptAnalysisMessage
    {
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
    }
}
