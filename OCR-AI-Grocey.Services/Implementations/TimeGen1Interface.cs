using Microsoft.Extensions.Logging;
using OCR_AI_Grocery.Models.Receipt;
using OCR_AI_Grocey.Services.Helpers;
using OCR_AI_Grocey.Services.Interfaces;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;

namespace OCR_AI_Grocey.Services.Implementations
{
    public class TimeGen1Interface : ITimeGen1Interface
    {
        private readonly IReceiptRepository _receiptRepository;
        private readonly IShoppingListRepository _shoppingListRepository;
        private readonly IOpenAIService _openAIService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<TimeGen1Interface> _logger;
        private readonly IAIMLInterface _analysisQueue;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _baseEndpoint;
        private readonly JsonSerializerOptions _jsonOptions;

        public TimeGen1Interface(
            IReceiptRepository receiptRepository,
            IShoppingListRepository shoppingListRepository,
            IOpenAIService openAIService,
            INotificationService notificationService,
            ILogger<TimeGen1Interface> logger,
            HttpClient httpClient)
        {
            _receiptRepository = receiptRepository;
            _shoppingListRepository = shoppingListRepository;
            _openAIService = openAIService;
            _notificationService = notificationService;
            _logger = logger;
            _httpClient = httpClient;
            _apiKey = "NxbdijU8hBCE874ywxFxesFF1pQVWYkz";
            _baseEndpoint = "https://TimeGEN-1-grocery.eastus.models.ai.azure.com";
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task TrainTheModel()
        {
            _logger.LogInformation("TrainTimeGen1HttpFunction HTTP trigger function processing a request.");

            try
            {
                // Step 1: Fetch and process receipt data
                var timeSeriesData = await FetchAndProcessReceipts();

                // Step 2: Prepare data for TimeGEN-1 API
                var formattedData = PrepareTimeSeriesData(timeSeriesData);

                // Step 3: Call the TimeGEN-1 API
                var (success, responseContent) = await CallTimeGen1Api(formattedData);

                // Step 4: Handle the response
                await HandleApiResponse(success, responseContent, timeSeriesData.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing receipts and triggering TimeGen1 forecast: {Message}", ex.Message);
                throw;
            }
        }

        private async Task<List<TimeSeriesDataPoint>> FetchAndProcessReceipts()
        {
            _logger.LogInformation("Querying receipts with TimeSeriesData...");
            var receipts = await _receiptRepository.FetchReceipts();
            var timeSeriesData = new List<TimeSeriesDataPoint>();
            var receiptCount = 0;

            foreach (var receipt in receipts)
            {
                if (!string.IsNullOrEmpty(receipt.TimeSeriesData))
                {
                    try
                    {
                        var receiptTimeSeriesData = ExtractTimeSeriesDataFromReceipt(receipt);
                        timeSeriesData.AddRange(receiptTimeSeriesData);
                        receiptCount++;
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Failed to parse TimeSeriesData for receipt {ReceiptId}: {Message}",
                            receipt.Id, ex.Message);
                    }
                }
            }

            _logger.LogInformation("Retrieved {ReceiptCount} receipts with {DataPointCount} time series data points",
                receiptCount, timeSeriesData.Count);

            return timeSeriesData;
        }

        private List<TimeSeriesDataPoint> ExtractTimeSeriesDataFromReceipt(ReceiptDocument receipt)
        {
            var extractedPoints = new List<TimeSeriesDataPoint>();

            // Parse TimeSeriesData JSON and extract data points
            var timeSeriesDict = JsonSerializer.Deserialize<Dictionary<string, List<TimeSeriesDataPoint>>>(
                receipt.TimeSeriesData,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (timeSeriesDict != null)
            {
                foreach (var daily in timeSeriesDict.Values)
                {
                    if (daily != null)
                    {
                        // Set UserEmail from receipt for tracking
                        foreach (var point in daily)
                        {
                            point.UserEmail = receipt.UserId;
                        }

                        extractedPoints.AddRange(daily);
                    }
                }
            }

            return extractedPoints;
        }

        private List<object> PrepareTimeSeriesData(List<TimeSeriesDataPoint> timeSeriesData, bool includeUserDimension = true, bool includeStoreDimension = true)
        {
            // Group data based on the selected dimensions
            var formattedData = new List<object>();

            if (includeUserDimension && includeStoreDimension)
            {
                // Group by User + Store + Item
                var groupedData = timeSeriesData
                    .Where(p => !string.IsNullOrEmpty(p.UserEmail) && !string.IsNullOrEmpty(p.Item))
                    .GroupBy(p => new {
                        User = p.UserEmail,
                        Store = string.IsNullOrEmpty(p.Store) ? "Unknown" : p.Store,
                        Item = p.Item
                    })
                    .ToDictionary(
                        g => $"{g.Key.User}_{g.Key.Store}_{g.Key.Item}",
                        g => g.OrderBy(p => p.Timestamp).ToList()
                    );

                formattedData = groupedData.SelectMany(group =>
                    group.Value.Select(point => new
                    {
                        unique_id = group.Key,
                        ds = point.Timestamp.HasValue ? point.Timestamp.Value.ToString("yyyy-MM-dd") : null,
                        y = point.Price,
                        user_email = point.UserEmail,
                        store = point.Store,
                        item = point.Item,
                        quantity = point.Quantity
                    })
                ).Cast<object>().ToList();
            }
            else if (includeStoreDimension)
            {
                // Group by Store + Item
                var groupedData = timeSeriesData
                    .Where(p => !string.IsNullOrEmpty(p.Item))
                    .GroupBy(p => new {
                        Store = string.IsNullOrEmpty(p.Store) ? "Unknown" : p.Store,
                        Item = p.Item
                    })
                    .ToDictionary(
                        g => $"{g.Key.Store}_{g.Key.Item}",
                        g => g.OrderBy(p => p.Timestamp).ToList()
                    );

                formattedData = groupedData.SelectMany(group =>
                    group.Value.Select(point => new
                    {
                        unique_id = group.Key,
                        ds = point.Timestamp.HasValue ? point.Timestamp.Value.ToString("yyyy-MM-dd") : null,
                        y = point.Price,
                        store = point.Store,
                        item = point.Item,
                        quantity = point.Quantity
                    })
                ).Cast<object>().ToList();
            }
            else if (includeUserDimension)
            {
                // Group by User + Item
                var groupedData = timeSeriesData
                    .Where(p => !string.IsNullOrEmpty(p.UserEmail) && !string.IsNullOrEmpty(p.Item))
                    .GroupBy(p => new {
                        User = p.UserEmail,
                        Item = p.Item
                    })
                    .ToDictionary(
                        g => $"{g.Key.User}_{g.Key.Item}",
                        g => g.OrderBy(p => p.Timestamp).ToList()
                    );

                formattedData = groupedData.SelectMany(group =>
                    group.Value.Select(point => new
                    {
                        unique_id = group.Key,
                        ds = point.Timestamp.HasValue ? point.Timestamp.Value.ToString("yyyy-MM-dd") : null,
                        y = point.Price,
                        user_email = point.UserEmail,
                        item = point.Item,
                        quantity = point.Quantity
                    })
                ).Cast<object>().ToList();
            }
            else
            {
                // Original logic - just group by Item
                var groupedData = timeSeriesData
                    .Where(p => !string.IsNullOrEmpty(p.Item))
                    .GroupBy(p => p.Item)
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderBy(p => p.Timestamp).ToList()
                    );

                formattedData = groupedData.SelectMany(group =>
                    group.Value.Select(point => new
                    {
                        unique_id = group.Key,
                        ds = point.Timestamp.HasValue ? point.Timestamp.Value.ToString("yyyy-MM-dd") : null,
                        y = point.Price,
                        item = point.Item,
                        quantity = point.Quantity
                    })
                ).Cast<object>().ToList();
            }

            _logger.LogInformation("Prepared time series data with {Count} data points using dimensions - User: {UserDim}, Store: {StoreDim}",
                formattedData.Count,
                includeUserDimension,
                includeStoreDimension);

            // Filter out any points with null dates or prices
            formattedData = formattedData
                .Where(p => {
                    var prop = p.GetType().GetProperty("ds");
                    var priceProp = p.GetType().GetProperty("y");
                    return prop?.GetValue(p) != null && priceProp?.GetValue(p) != null;
                })
                .ToList();

            if (formattedData.Count == 0)
            {
                _logger.LogWarning("No valid data points found after filtering nulls");
            }

            return formattedData;
        }

        private async Task<(bool success, string responseContent)> CallTimeGen1Api(List<object> formattedData)
        {
            // Create the request payload with the correct field names
            var standardRequest = CreateStandardRequest(formattedData);
            var alternativeRequest = CreateAlternativeRequest(formattedData);

            // Configure HTTP client
            ConfigureHttpClient();

            // Try to call API with standard format
            var (success, responseContent) = await TryApiEndpoints(standardRequest);

            // If standard format failed, try alternative format
            if (!success)
            {
                _logger.LogWarning("All endpoints failed with standard format. Trying alternative format.");
                (success, responseContent) = await TryApiEndpoints(alternativeRequest);
            }

            return (success, responseContent);
        }

        private object CreateStandardRequest(List<object> formattedData)
        {
            return new
            {
                series = formattedData,
                h = 7,
                freq = "D",
                level = new[] { 80 },
                model = "azureai"
            };
        }

        private object CreateAlternativeRequest(List<object> formattedData)
        {
            return new
            {
                data = formattedData,
                horizon = 7,
                h = 7,
                frequency = "D",
                freq = "D",
                confidence_level = new[] { 80 }
            };
        }

        private void ConfigureHttpClient()
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        private async Task<(bool success, string responseContent)> TryApiEndpoints(object request)
        {
            string[] endpointPaths = new[] {
                "/forecast_multi_series",
                "/v2/forecast_multi_series",
                "/forecast",
                "/v2/forecast"
            };

            var jsonContent = JsonSerializer.Serialize(request, _jsonOptions);

            foreach (var path in endpointPaths)
            {
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                string endpointUrl = $"{_baseEndpoint.TrimEnd('/')}{path}";
                _logger.LogInformation("Trying endpoint: {Endpoint}", endpointUrl);

                try
                {
                    var apiResponse = await _httpClient.PostAsync(endpointUrl, content);

                    if (apiResponse.IsSuccessStatusCode)
                    {
                        var responseContent = await apiResponse.Content.ReadAsStringAsync();
                        _logger.LogInformation("Successfully triggered TimeGen1 forecast using endpoint {Endpoint}.", path);
                        return (true, responseContent);
                    }

                    var errorContent = await apiResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning("Endpoint {Endpoint} failed with status {Status}: {Error}",
                        path, apiResponse.StatusCode, errorContent);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "HTTP request error with endpoint {Endpoint}: {Message}", path, ex.Message);
                }
            }

            return (false, null);
        }

        private async Task HandleApiResponse(bool success, string responseContent, int dataPointCount)
        {
            if (success)
            {
                _logger.LogInformation("TimeGen1 forecast completed successfully");
                // Here you would handle a successful response, e.g.:
                // await ProcessForecastResults(responseContent);
            }
            else
            {
                _logger.LogError("Failed to trigger TimeGen1 forecast after trying all formats and endpoints");
                throw new HttpRequestException("Failed to process time series data. Check function logs for details.");
            }
        }
    }
}