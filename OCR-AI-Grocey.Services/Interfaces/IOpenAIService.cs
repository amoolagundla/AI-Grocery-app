using OCR_AI_Grocery.Models.Receipt;
using OCR_AI_Grocey.Services.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Interfaces
{
    public interface IOpenAIService
    {
        Task<(Dictionary<string, List<string>>, Dictionary<DateTime, List<TimeSeriesDataPoint>>)> AnalyzeReceiptsWithOpenAI(List<ReceiptDocument> receipts);
        Task<string> NormalizeStoreName(string receiptText);
        Task<Dictionary<string, List<string>>> AnalyzeReceiptWithOpenAIAsync(string receipt);
    }
}
