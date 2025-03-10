using OCR_AI_Grocery.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Interfaces
{
    public interface IShoppingListRepository
    { 

        /// <summary>
        /// Gets an existing shopping list for a family, or creates a new one if none exists
        /// </summary>
        /// <param name="familyId">The family ID to get the shopping list for</param>
        /// <returns>The shopping list</returns>
        Task<ShoppingList> GetExistingShoppingList(string familyId);

        /// <summary> 
        /// Gets all shopping lists for a family
        /// </summary>
        /// <param name="userEmail">The family ID/user email to get shopping lists for</param>
        /// <returns>A list of shopping lists</returns>
        Task<List<ShoppingList>> GetShoppingListsForFamily(string userEmail);

        /// <summary>
        /// Updates a shopping list
        /// </summary>
        /// <param name="shoppingList">The shopping list to update</param>
        Task UpdateShoppingList(ShoppingList shoppingList);
    }
}
