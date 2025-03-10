using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.Family.models
{
    public class FamilyJunction
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty; // Email as ID
        [JsonProperty("partitionKey")]
        public string PartitionKey { get; set; }  // This should match FamilyId
        [JsonProperty("FamilyId")]
        public string FamilyId { get; set; } = string.Empty;

        [JsonProperty("email")]
        public string Email { get; set; } = string.Empty;

        [JsonProperty("joinDate")]
        public DateTime JoinDate { get; set; } = DateTime.UtcNow;

        [JsonProperty("status")]
        public string Status { get; set; } = "Active";
    }
}
