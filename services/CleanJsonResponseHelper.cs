using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace OCR_AI_Grocery.services
{
    public class CleanJsonResponseHelper
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

                // Step 3: Ensure all remaining single quotes are converted to double quotes
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
    }
}