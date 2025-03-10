using Newtonsoft.Json;
using System;

namespace OCR_AI_Grocery.Models.Receipt
{
    public class ReceiptDocument
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();  // CosmosDB requires an "id" field

        [JsonProperty("FamilyId")]
        public string FamilyId { get; set; }  // ✅ Partition Key (MUST MATCH)

        [JsonProperty("UserId")]
        public string UserId { get; set; }

        [JsonProperty("ReceiptText")]
        public string ReceiptText { get; set; }

        [JsonProperty("StoreName")]
        public string StoreName { get; set; }

        [JsonProperty("storeItems")]
        public Dictionary<string, List<string>> StoreItems { get; set; }

        [JsonProperty("UploadDate")]
        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        [JsonProperty("BlobUrl")]
        public string BlobUrl { get; set; }
        public DateTime PurchasedDate { get;  set; }

        public bool Processed { get; set; } = false;
    }
}