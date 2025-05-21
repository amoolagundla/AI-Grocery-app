using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OCR_AI_Grocery.services;

namespace OCR_AI_Grocey.Services.Helpers
{
    public class JsonResponseParser : IJsonResponseParser
    {
        private readonly ILogger<JsonResponseParser> _logger;
        private readonly ICleanJsonResponseHelper _cleanJsonResponseHelper;

        public JsonResponseParser(ILogger<JsonResponseParser> logger, ICleanJsonResponseHelper cleanJsonResponseHelper = null)
        {
            _logger = logger;
            _cleanJsonResponseHelper = cleanJsonResponseHelper;
        }

        /// <summary>
        /// Parses OpenAI response and returns the original dictionary structure
        /// </summary>
        public Dictionary<string, StoreData> ParseOpenAIResponse(string responseString)
        {
            try
            {
                _logger.LogInformation("Processing OpenAI response");

                // Extract the content from OpenAI response format
                var extractedJson = ExtractJsonContent(responseString);
                _logger.LogInformation($"Extracted JSON: {extractedJson}");

                // Deserialize into our model
                var result = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(extractedJson);

                if (result == null)
                {
                    throw new InvalidOperationException("Failed to deserialize JSON");
                }

                // Convert to our format
                var storeData = new Dictionary<string, StoreData>();

                foreach (var store in result)
                {
                    var storeName = store.Key;
                    if (storeName == "stores")
                    {
                        // Handle nested structure
                        var storesObj = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(
                            JsonConvert.SerializeObject(store.Value));

                        foreach (var nestedStore in storesObj)
                        {
                            storeData[nestedStore.Key] = ParseStoreData(nestedStore.Value);
                        }
                    }
                    else
                    {
                        // Handle flat structure
                        storeData[storeName] = ParseStoreData(store.Value);
                    }
                }

                return storeData;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing OpenAI response: {ex.Message}");

                // Fallback to helper if available
                if (_cleanJsonResponseHelper != null)
                {
                    try
                    {
                        return _cleanJsonResponseHelper.CleanAndParseJson<Dictionary<string, StoreData>>(responseString);
                    }
                    catch (Exception innerEx)
                    {
                        _logger.LogError($"Failed to clean JSON: {innerEx.Message}");
                    }
                }

                throw;
            }
        }

        /// <summary>
        /// Parses OpenAI response and returns a time series friendly format
        /// </summary>
        public Dictionary<string, List<TimeSeriesDataPoint>> ParseOpenAIResponseForTimeSeries(string responseString)
        {
            var storeData = ParseOpenAIResponse(responseString);
            var timeSeriesFormat = new Dictionary<string, List<TimeSeriesDataPoint>>();

            foreach (var store in storeData)
            {
                var storeName = store.Key;
                var data = store.Value;
                var timeSeriesPoints = new List<TimeSeriesDataPoint>();

                if (data.Items != null && data.Prices != null)
                {
                    for (int i = 0; i < Math.Min(data.Items.Count, data.Prices.Count); i++)
                    {
                        timeSeriesPoints.Add(new TimeSeriesDataPoint
                        {
                            Timestamp = data.PurchaseDate==string.Empty ? null : DateTime.Parse(data.PurchaseDate),
                            Item = data.Items[i],
                            Price = data.Prices[i],
                            TransactionId = data.TransactionId
                        });
                    }
                }

                timeSeriesFormat[storeName] = timeSeriesPoints;
            }

            return timeSeriesFormat;
        }

        private string ExtractJsonContent(string responseString)
        {
            try
            {
                // First try parsing as OpenAI response
                var openAIResponse = JsonConvert.DeserializeObject<OpenAIResponse>(responseString);
                if (openAIResponse?.Choices != null && openAIResponse.Choices.Any() &&
                    !string.IsNullOrEmpty(openAIResponse.Choices[0].Message.Content))
                {
                    return CleanJsonResponse(openAIResponse.Choices[0].Message.Content);
                }
            }
            catch
            {
                // If not a standard OpenAI response, continue
            }

            // Try to extract JSON directly if it's not in OpenAI format
            return CleanJsonResponse(responseString);
        }

        private string CleanJsonResponse(string response)
        {
            // Remove markdown code block markers if present
            response = Regex.Replace(response, @"```(?:json)?\s*|\s*```", "");

            // Try to extract JSON objects
            var jsonMatch = Regex.Match(response, @"\{[\s\S]*\}");
            if (jsonMatch.Success)
            {
                try
                {
                    // Validate JSON is well-formed
                    var testParse = JObject.Parse(jsonMatch.Value);
                    return jsonMatch.Value;
                }
                catch
                {
                    // If not valid JSON, return cleaned string
                }
            }

            return response;
        }

        private StoreData ParseStoreData(Dictionary<string, object> storeDict)
        {
            var result = new StoreData();

            foreach (var kvp in storeDict)
            {
                switch (kvp.Key.ToLower())
                {
                    case "items":
                        result.Items = ConvertToList<string>(kvp.Value ?? new List<string>());
                        break;
                    case "prices":
                        result.Prices = ConvertToList<double?>(kvp.Value?? new List<double?>());
                        break;
                    case "purchase_date":
                        result.PurchaseDate = (string?)(kvp.Value ?? string.Empty);
                        break;
                    case "transaction_id":
                        result.TransactionId = (string?)(kvp.Value ?? string.Empty);
                        break;
                }
            }

            return result;
        }

        private List<T> ConvertToList<T>(object obj)
        {
            if (obj is JArray jArray)
            {
                return jArray.ToObject<List<T>>();
            }
            else if (obj is IEnumerable<object> enumerable)
            {
                return enumerable.Select(item => (T)Convert.ChangeType(item, typeof(T))).ToList();
            }
            else
            {
                return JsonConvert.DeserializeObject<List<T>>(JsonConvert.SerializeObject(obj));
            }
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
