using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCR_AI_Grocery.Models.Receipt;
using OCR_AI_Grocery.services;
using OCR_AI_Grocey.Services.Interfaces;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace OCR_AI_Grocey.Services.Implementations
{
    public class OpenAIService : IOpenAIService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenAIService> _logger;
        private readonly CleanJsonResponseHelper _cleanJsonResponseHelper;

        public OpenAIService(
            HttpClient httpClient,
            ILoggerFactory loggerFactory,
            CleanJsonResponseHelper cleanJsonResponseHelper)
        {
            _httpClient = httpClient;
            _logger = loggerFactory.CreateLogger<OpenAIService>();
            _cleanJsonResponseHelper = cleanJsonResponseHelper;
        }

        public async Task<Dictionary<string, List<string>>> AnalyzeReceiptsWithOpenAI(List<ReceiptDocument> receipts)
        {
            var allReceiptsText = string.Join("\n\n", receipts.Select(r => r.ReceiptText));
            var prompt = GenerateOpenAIPrompt(allReceiptsText);

            trygpt

            {
                var openAiKey = Environment.GetEnvironmentVariable("OpenAI_API_Key")
                    ?? throw new InvalidOperationException("OpenAI API Key not found");

                var requestBody = CreateOpenAIRequest(prompt);
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", openAiKey);

                var response = await _httpClient.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", requestBody);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                return ParseOpenAIResponse(responseString);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error analyzing receipts with OpenAI: {ex.Message}");
                throw;
            }
        }

        public async Task<string> NormalizeStoreName(string receiptText)
        {
            try
            {
                var prompt = $@"Extract and normalize the store name from this receipt text. 
                Follow these rules:
                1. Remove special characters
                2. Replace apostrophes with 's'
                3. Return ONLY the store name, nothing else
                4. If no store name is found, return 'Unknown Store'

                Receipt text:
                {receiptText}";

                var openAiKey = Environment.GetEnvironmentVariable("OpenAI_API_Key")
                    ?? throw new InvalidOperationException("OpenAI API Key not found");

                var requestBody = CreateOpenAIRequest(prompt);
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", openAiKey);

                var response = await _httpClient.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", requestBody);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                var parsedResponse = JsonConvert.DeserializeObject<OpenAIResponse>(responseString);

                var storeName = parsedResponse?.Choices?.FirstOrDefault()?.Message?.Content?.Trim()
                    ?? "Unknown Store";

                // Clean up the store name
                storeName = Regex.Replace(storeName, @"[^a-zA-Z0-9\s]", "").Trim();
                return string.IsNullOrEmpty(storeName) ? "Unknown Store" : storeName;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error normalizing store name with OpenAI: {ex.Message}");
                return "Unknown Store";
            }
        }

        private string GenerateOpenAIPrompt(string receiptsText) =>
    $@"Analyze these receipts and create a structured shopping list. Follow these rules exactly:

    1. Format store names:
       - Replace apostrophes with 's'
       - Remove special characters
       - Example: 'Sam's Club' → 'Sams Club'
       - Combine stores with different locations into a single entry (e.g., 'Walmart Supercenter (Downtown)' and 'Walmart Supercenter (Uptown)' should both be 'Walmart Supercenter').

    2. Format item names:
       - Expand abbreviations
       - Use common names
       - Include brand names when available
       - Examples:
         'CASCPLATPLUS' → 'Cascade Platinum Plus Dishwasher Detergent'
         'OJ' → 'Orange Juice'

    3. Return ONLY valid JSON in this exact format:
    {{
        ""StoreName"": [
            ""Item1"",
            ""Item2""
        ]
    }}

    4. Do NOT include:
       - Prices
       - Quantities
       - Extra text or explanations
       - Markdown formatting

    Receipts to analyze:
    {receiptsText}";


        private object CreateOpenAIRequest(string prompt) => new
        {
            model = "gpt-4-turbo",
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = "You are a precise JSON formatter. Return only valid JSON without any additional text or explanations."
                },
                new
                {
                    role = "user",
                    content = prompt
                }
            },
            temperature = 0.3,
            max_tokens = 1000
        };

        private Dictionary<string, List<string>> ParseOpenAIResponse(string responseString)
        {
            try
            {
                var openAIResponse = JsonConvert.DeserializeObject<OpenAIResponse>(responseString);
                if (openAIResponse?.Choices == null || !openAIResponse.Choices.Any())
                {
                    throw new InvalidOperationException("No choices found in OpenAI response");
                }

                var aiGeneratedText = openAIResponse.Choices[0].Message.Content;
                _logger.LogInformation($"AI Generated Text: {aiGeneratedText}");

                // Clean and parse the JSON response
                string cleanedJson = CleanJsonResponse(aiGeneratedText);
                var result = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(cleanedJson);

                return result ?? new Dictionary<string, List<string>>();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing OpenAI response: {ex.Message}");
                try
                {
                    // Fallback to using CleanJsonResponseHelper
                    return _cleanJsonResponseHelper.CleanAndParseJson<Dictionary<string, List<string>>>(responseString)
                        ?? new Dictionary<string, List<string>>();
                }
                catch (Exception innerEx)
                {
                    _logger.LogError($"Failed to clean JSON: {innerEx.Message}");
                    throw;
                }
            }
        }

        private string CleanJsonResponse(string response)
        {
            // Remove markdown code block markers if present
            response = Regex.Replace(response, @"```json\s*|\s*```", "");

            // Extract just the JSON object if there's extra text
            var jsonMatch = Regex.Match(response, @"\{[\s\S]*\}");
            return jsonMatch.Success ? jsonMatch.Value : response;
        }

        private class OpenAIResponse
        {
            [JsonProperty("choices")]
            public List<Choice> Choices { get; set; } = new();

            public class Choice
            {
                [JsonProperty("message")]
                public Message Message { get; set; } = new();
            }

            public class Message
            {
                [JsonProperty("content")]
                public string Content { get; set; } = string.Empty;
            }
        }
    }
}