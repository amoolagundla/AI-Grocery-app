using OCR_AI_Grocery.Models.Receipt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Interfaces
{
    public interface IReceiptService
    {
        Task SaveReceiptAsync(ReceiptDocument receipt);
        // Add other receipt-related operations as needed
    }
}
