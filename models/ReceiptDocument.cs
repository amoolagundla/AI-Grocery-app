using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.models
{
    public class ReceiptDocument
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("familyId")]
        public string FamilyId { get; set; } = string.Empty;

        [JsonProperty("userId")]
        public string UserId { get; set; } = string.Empty;

        [JsonProperty("receiptText")]
        public string ReceiptText { get; set; } = string.Empty;

        [JsonProperty("storeName")]
        public string StoreName { get; set; } = string.Empty;

        [JsonProperty("blobUrl")]
        public string BlobUrl { get; set; } = string.Empty;

        [JsonProperty("uploadDate")]
        public DateTime UploadDate { get; set; }
        public DateTime PurchasedDate { get; internal set; }
        public bool Processed { get; internal set; }
    }
}
