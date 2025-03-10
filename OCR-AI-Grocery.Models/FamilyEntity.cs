using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.Family.models
{
    public class FamilyEntity
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("FamilyId")]
        public string FamilyId { get; set; } // Added for partition key

        [JsonProperty("familyName")]
        public string FamilyName { get; set; }

        [JsonProperty("primaryEmail")]
        public string PrimaryEmail { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
