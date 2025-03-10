using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Helpers
{
    public static class StoreNameNormalizer
    {
        public static string NormalizeStoreName(string storeName)
        {
            if (string.IsNullOrEmpty(storeName))
                return "Unknown Store";

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
    }
}
