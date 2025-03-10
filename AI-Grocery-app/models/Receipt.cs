using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.models
{
    public class Receipt
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;  // User email as UserId
        public string FamilyId { get; set; } = string.Empty;
        public string ReceiptText { get; set; } = string.Empty;
        public string BlobUrl { get; set; } = string.Empty; // URL to receipt image
        public string StoreName { get; set; } = string.Empty; 
        public DateTime CreatedAt { get; set; }
    }
}
