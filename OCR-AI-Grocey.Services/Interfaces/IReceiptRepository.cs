using OCR_AI_Grocery.Models.Receipt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Interfaces
{
    public interface IReceiptRepository
    {
        Task<List<ReceiptDocument>> FetchUnprocessedReceipts(string familyId);
        Task UpdateReceipt(ReceiptDocument receipt);
        Task<List<ReceiptDocument>> FetchRecipet(string receiptId);
        Task<List<ReceiptDocument>> FetchReceipts( );
    }
}
