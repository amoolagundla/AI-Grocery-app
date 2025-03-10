using OCR_AI_Grocery.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Helpers
{
    public static class ShoppingListExtensions
    {
        public static ShoppingList CreateMergedCopy(this ShoppingList existing)
        {
            return new ShoppingList
            {
                Id = existing.Id,
                UserId = existing.UserId,
                FamilyId = existing.FamilyId,
                StoreItems = new Dictionary<string, List<string>>(existing.StoreItems),
                CreatedAt = existing.CreatedAt
            };
        }

        public static ShoppingList AddNewItems(
            this ShoppingList list,
            Dictionary<string, List<string>> newItems,
            Func<string, string> normalizeStoreName)
        {
            foreach (var (store, items) in newItems)
            {
                var normalizedStore = normalizeStoreName(store);
                if (!list.StoreItems.ContainsKey(normalizedStore))
                {
                    list.StoreItems[normalizedStore] = new List<string>();
                }

                list.StoreItems[normalizedStore].AddRange(
                    items.Where(item => !list.StoreItems[normalizedStore]
                        .Contains(item, StringComparer.OrdinalIgnoreCase))
                );
            }
            return list;
        }

        public static ShoppingList WithUpdatedTimestamp(this ShoppingList list)
        {
            list.LastUpdated = DateTime.UtcNow;
            return list;
        }
    }
}
