using OCR_AI_Grocery.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Interfaces
{
    public interface INotificationService
    {
        Task SendNotification(ReceiptAnalysisMessage message, Dictionary<string, List<string>> newItems, ShoppingList shoppingList, string storeName);
    }
}
