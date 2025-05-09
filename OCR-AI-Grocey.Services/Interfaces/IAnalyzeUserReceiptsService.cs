using Azure.Messaging.ServiceBus;
using OCR_AI_Grocery.Models;
using OCR_AI_Grocery.Models.Receipt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Interfaces
{
    public interface IAnalyzeUserReceiptsService
    {
        Task ProcessReceiptAnalysis(ServiceBusReceivedMessage message);
        Task<ShoppingList> GetExistingShoppingList(string familyId);
        Task<Dictionary<string, List<string>>> AnalyzeReceiptsWithOpenAI(List<ReceiptDocument> receipts);
        Task<List<ReceiptDocument>> FetchUnprocessedReceipts(string familyId);
        Task<Dictionary<string, List<string>>> AnalyzeReceiptWithOpenAI(string receiptId);
        Task UpdateShoppingLists(ReceiptAnalysisMessage receiptAnalysis);
    }
}
