using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Text.RegularExpressions;

namespace OCR_AI_Grocery.services
{
    public class CleanJsonResponseHelper : ICleanJsonResponseHelper
    {
        private readonly ILogger _logger;

        public CleanJsonResponseHelper(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<CleanJsonResponseHelper>();
        }

        public string CleanJsonResponse(string response)
        {
            try
            {
                _logger.LogInformation("🔍 Cleaning AI JSON Response...");

                // Remove markdown code blocks if present
                response = Regex.Replace(response, @"```json\s*|\s*```", "");

                // Extract JSON content
                int startIndex = response.IndexOf('{');
                int endIndex = response.LastIndexOf('}');
                if (startIndex == -1 || endIndex == -1)
                {
                    _logger.LogWarning("❌ JSON structure incorrect. Returning empty.");
                    return "{}";
                }

                response = response.Substring(startIndex, endIndex - startIndex + 1);

                // Step 1: Fix escaped quotes
                response = response.Replace("\\\"", "'");

                // Step 2: Fix property names with apostrophes
                response = FixPropertyNames(response);

                // Step 3: Fix unterminated strings (a common issue)
                response = FixUnterminatedStrings(response);

                // Step 4: Ensure all remaining single quotes are converted to double quotes
                response = response.Replace("'", "\"");

                _logger.LogInformation($"✅ Cleaned JSON Response: {response}");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error in cleaning JSON: {ex.Message}");
                return "{}";
            }
        }

        private string FixPropertyNames(string json)
        {
            try
            {
                // Pattern to match property names that might contain apostrophes
                // Matches: "PropertyName"s" or "PropertyName\"s"
                var pattern = @"""([^""]+)""s""(?=\s*:)";
                return Regex.Replace(json, pattern, match =>
                {
                    // Get the property name without the surrounding quotes
                    var propertyName = match.Groups[1].Value;
                    // Return the properly formatted property name with apostrophe
                    return $"\"{propertyName}'s\"";
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error in fixing property names: {ex.Message}");
                return json;
            }
        }

        private string FixUnterminatedStrings(string json)
        {
            try
            {
                // This is a simplified approach to fix common unterminated strings
                // For a more robust solution, a proper JSON parser might be needed

                // Pattern to find property values that might be missing closing quotes
                var pattern = @"""([^""]*?)""?\s*,";

                return Regex.Replace(json, pattern, match =>
                {
                    // Ensure there's a proper closing quote before the comma
                    return $"\"{match.Groups[1].Value}\",";
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error in fixing unterminated strings: {ex.Message}");
                return json;
            }
        }

        public T CleanAndParseJson<T>(string jsonResponse) where T : new()
        {
            try
            {
                // First clean the JSON
                string cleanedJson = CleanJsonResponse(jsonResponse);

                // Try to parse the cleaned JSON
                try
                {
                    return JsonConvert.DeserializeObject<T>(cleanedJson) ?? new T();
                }
                catch (JsonException firstEx)
                {
                    _logger.LogWarning($"⚠️ First JSON parse attempt failed: {firstEx.Message}");

                    // Last resort - try with more tolerant settings
                    var settings = new JsonSerializerSettings
                    {
                        Error = (sender, args) =>
                        {
                            _logger.LogWarning($"JSON error suppressed: {args.ErrorContext.Error.Message}");
                            args.ErrorContext.Handled = true;
                        },
                        MissingMemberHandling = MissingMemberHandling.Ignore,
                        NullValueHandling = NullValueHandling.Ignore
                    };

                    return JsonConvert.DeserializeObject<T>(cleanedJson, settings) ?? new T();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Failed to clean and parse JSON: {ex.Message}");
                return new T();
            }
        }
    }
}