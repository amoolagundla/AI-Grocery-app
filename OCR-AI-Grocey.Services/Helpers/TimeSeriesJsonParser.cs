using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Cosmos.Linq;
using Newtonsoft.Json;

namespace OCR_AI_Grocey.Services.Helpers
{
    public class TimeSeriesJsonParser
    {
        /// <summary>
        /// Converts the original store data format to a time series format suitable for ML algorithms
        /// </summary>
        public static Dictionary<string, List<TimeSeriesDataPoint>> ConvertToTimeSeriesFormat(string jsonContent,string userEmail)
        {
            // Parse the original JSON
            var originalData = JsonConvert.DeserializeObject<StoreDataWrapper>(jsonContent);
            if (originalData?.Stores == null)
            {
                throw new InvalidOperationException("Invalid store data format");
            }

            var result = new Dictionary<string, List<TimeSeriesDataPoint>>();

            // Process each store
            foreach (var store in originalData.Stores)
            {
                var storeName = store.Key;
                var storeData = store.Value;
                var timeSeriesPoints = new List<TimeSeriesDataPoint>();

                // Create time series data points
                if (storeData.Items != null && storeData.Prices != null)
                {
                    var itemCount = Math.Min(storeData.Items.Count, storeData.Prices.Count);
                    for (int i = 0; i < itemCount; i++)
                    {
                        var date = DateTime.Parse(storeData.PurchaseDate);

                        timeSeriesPoints.Add(new TimeSeriesDataPoint
                        {
                            Timestamp = date,
                            UnixTimestamp = new DateTimeOffset(date).ToUnixTimeMilliseconds(),
                            Store = storeName,
                            Item = storeData.Items[i],
                            Price = storeData.Prices[i],
                            TransactionId = storeData.TransactionId,
                            UserEmail= userEmail,
                            ItemCategory = CategorizeItem(storeData.Items[i]),
                            DayOfWeek = (int)date.DayOfWeek,
                            Month = date.Month,
                            Year = date.Year,
                            DayOfMonth = date.Day,
                            WeekOfYear = GetWeekOfYear(date),
                            IsWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday
                        });
                    }
                }

                result[storeName] = timeSeriesPoints;
            }

            return result;
        }

        /// <summary>
        /// Returns a flattened list format that's often better for ML algorithms
        /// </summary>
        public static List<TimeSeriesDataPoint> GetFlattenedFormat(Dictionary<string, List<TimeSeriesDataPoint>> timeSeriesData)
        {
            var flattened = new List<TimeSeriesDataPoint>();

            foreach (var store in timeSeriesData)
            {
                flattened.AddRange(store.Value);
            }

            return flattened;
        }

        /// <summary>
        /// Returns a grouped format that's useful for time series analysis
        /// </summary>
        public static Dictionary<DateTime, List<TimeSeriesDataPoint>> GetGroupedByDateFormat(
            Dictionary<string, List<TimeSeriesDataPoint>> timeSeriesData)
        {
            var flattened = GetFlattenedFormat(timeSeriesData);
            return flattened.GroupBy(p => p.Timestamp.Value.Date)
                            .ToDictionary(g => g.Key, g => g.ToList());
        }

        /// <summary>
        /// Simple categorization function - can be expanded with more categories
        /// </summary>
        private static string CategorizeItem(string itemName)
        {
            var lowerName = itemName.ToLower();
            if (lowerName.Contains("bread") || lowerName.Contains("bagel") || lowerName.Contains("pastry"))
                return "bakery";
            if (lowerName.Contains("banana") || lowerName.Contains("apple") ||
                lowerName.Contains("fruit") || lowerName.Contains("vegetable") ||
                lowerName.Contains("organic"))
                return "produce";
            if (lowerName.Contains("milk") || lowerName.Contains("cheese") || lowerName.Contains("yogurt"))
                return "dairy";
            if (lowerName.Contains("meat") || lowerName.Contains("chicken") || lowerName.Contains("beef"))
                return "meat";
            return "other";
        }

        /// <summary>
        /// Calculate ISO week number
        /// </summary>
        private static int GetWeekOfYear(DateTime date)
        {
            return System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                date,
                System.Globalization.CalendarWeekRule.FirstFourDayWeek,
                DayOfWeek.Monday);
        }
    }

    public class StoreDataWrapper
    {
        [JsonProperty("stores")]
        public Dictionary<string, StoreData> Stores { get; set; }
    }

    public class StoreData
    {
        [JsonProperty("items")] 
        public List<string> Items { get; set; } = new List<string>();

        [JsonProperty("prices")]
        public List<double> Prices { get; set; } = new List<double>();

        [JsonProperty("purchase_date")]
        public string? PurchaseDate { get; set; }

        [JsonProperty("transaction_id")]
        public string? TransactionId { get; set; }

        [JsonProperty("user_email")]
        public string? UserEmail { get; set; }
    }
    public class TimeSeriesPayload : Dictionary<string, List<TimeSeriesDataPoint>>
    {
        /// <summary>
        /// Gets all data points across all dates.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<TimeSeriesDataPoint> AllPoints => this.SelectMany(kvp => kvp.Value);

        /// <summary>
        /// Gets all unique items across all dates.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<string> UniqueItems => AllPoints.Select(p => p.Item).Where(i => !string.IsNullOrWhiteSpace(i)).Distinct();

        /// <summary>
        /// Groups all points by item name.
        /// </summary>
        public Dictionary<string, List<TimeSeriesDataPoint>> GroupByItem()
        {
            return AllPoints
                .Where(p => !string.IsNullOrWhiteSpace(p.Item))
                .GroupBy(p => p.Item)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

         
    }

    /// <summary>
    /// Time series data format optimized for ML algorithms
    /// </summary>
    public class TimeSeriesDataPoint
    {
        // Primary Data
        public DateTime? Timestamp { get; set; }
        public long UnixTimestamp { get; set; } // Useful for many ML algorithms
        public string Store { get; set; }
        public string Item { get; set; }
        public double Price { get; set; }
        public string? TransactionId { get; set; }

        // Derived Features (useful for ML)
        public string ItemCategory { get; set; }
        public int DayOfWeek { get; set; }     // 0 = Sunday, 1 = Monday, etc.
        public int Month { get; set; }          // 1-12
        public int Year { get; set; }
        public int DayOfMonth { get; set; }     // 1-31
        public int WeekOfYear { get; set; }     // 1-53
        public bool IsWeekend { get; set; }

        // Additional fields that could be added if data available
        public int? Quantity { get; set; }      // Optional quantity if available
        public string UserEmail { get; set; }
    }
}
