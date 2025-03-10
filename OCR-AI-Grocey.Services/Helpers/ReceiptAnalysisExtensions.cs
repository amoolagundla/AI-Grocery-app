using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;
using OCR_AI_Grocery.Models.Receipt;
using OCR_AI_Grocery.Models;
using OCR_AI_Grocey.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Helpers
{
    public static class ReceiptAnalysisExtensions
    {
        public static ReceiptAnalysisMessage ThrowIfMissingFamilyId(this ReceiptAnalysisMessage message)
        {
            if (string.IsNullOrEmpty(message?.FamilyId))
            {
                throw new ArgumentException("Missing FamilyId in message");
            }
            return message;
        }

        public static bool HasReceipts(this List<ReceiptDocument> receipts)
        {
            return receipts != null && receipts.Any();
        }

        public static ReceiptAnalysisMessage ValidateAndExtractMessage(this ServiceBusReceivedMessage message)
        {
            return JsonConvert.DeserializeObject<ReceiptAnalysisMessage>(message?.Body?.ToString())?? new ReceiptAnalysisMessage();
        } 

        public static Dictionary<string, List<string>> ThrowIfNoItemsExtracted(
            this Task<Dictionary<string, List<string>>> itemsTask)
        {
            var items = itemsTask.Result;
            if (items == null || !items.Any())
            {
                throw new InvalidOperationException("No items extracted from receipts");
            }
            return items;
        }

        public static ReceiptDocument MarkAsProcessed(this ReceiptDocument receipt)
        {
            receipt.Processed = true;
            receipt.UploadDate = DateTime.UtcNow;
            return receipt;
        }

        public static ReceiptDocument WithStoreItems(
            this ReceiptDocument receipt,
            Dictionary<string, List<string>> items)
        {
            receipt.StoreItems = items;
            return receipt;
        }

        public static ReceiptDocument WithStoreName(
            this ReceiptDocument receipt,
            string storeName)
        {
            receipt.StoreName = storeName;
            return receipt;
        }

        public static async Task SaveAsync(
            this ReceiptDocument receipt,
            IReceiptRepository repository)
        {
            await repository.UpdateReceipt(receipt);
        }
    }
}
