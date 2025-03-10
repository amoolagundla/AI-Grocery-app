using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Helpers
{
    public static class StoreNameExtensions
    {
        public static string NormalizeStoreName(this string storeName, ILogger logger)
        {
            if (string.IsNullOrEmpty(storeName))
                return "Unknown Store";

            try
            {
                // Remove special characters except spaces
                storeName = Regex.Replace(storeName, @"[^a-zA-Z0-9\s]", "");

                // Replace multiple spaces with single space
                storeName = Regex.Replace(storeName, @"\s+", " ");

                // Convert to title case
                storeName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(storeName.ToLower());

                // Trim spaces
                storeName = storeName.Trim();

                return string.IsNullOrWhiteSpace(storeName) ? "Unknown Store" : storeName;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, $"Error normalizing store name: {storeName}");
                return "Unknown Store";
            }
        }

        public static bool IsSimilarStoreName(this string storeName1, string storeName2)
        {
            if (string.IsNullOrEmpty(storeName1) || string.IsNullOrEmpty(storeName2))
                return false;

            // Normalize both names
            var name1 = storeName1.ToLower().Trim();
            var name2 = storeName2.ToLower().Trim();

            // Direct match
            if (name1 == name2)
                return true;

            // Check if one contains the other
            if (name1.Contains(name2) || name2.Contains(name1))
                return true;

            // Calculate Levenshtein distance
            var distance = ComputeLevenshteinDistance(name1, name2);
            var maxLength = Math.Max(name1.Length, name2.Length);
            var similarity = 1 - ((double)distance / maxLength);

            // Consider similar if 80% or more similar
            return similarity >= 0.8;
        }

        private static int ComputeLevenshteinDistance(string s1, string s2)
        {
            var costs = new int[s1.Length + 1];
            for (int i = 0; i <= s1.Length; i++)
                costs[i] = i;

            for (int j = 1; j <= s2.Length; j++)
            {
                var cost = j;
                var previousCost = costs[0];

                for (int i = 1; i <= s1.Length; i++)
                {
                    var temp = costs[i];
                    costs[i] = Math.Min(
                        Math.Min(costs[i - 1] + 1, cost + 1),
                        previousCost + (s1[i - 1] == s2[j - 1] ? 0 : 1)
                    );
                    previousCost = temp;
                }
            }

            return costs[s1.Length];
        }
    }
}
