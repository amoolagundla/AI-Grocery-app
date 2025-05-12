using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.Models
{
    public class ShoppingListUpdate
    {
        public ShoppingList ExistingList { get; set; }
        public Dictionary<string, List<string>> NewItems { get; set; }
        public string StoreName { get; set; }
        public ShoppingList MergedList { get; set; }
        public string TimeSeriesData { get; set; }
    }
}
