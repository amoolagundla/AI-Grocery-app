using Newtonsoft.Json;
using System;

namespace OCR_AI_Grocery.Models
{
    public class PredictionDocument
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("familyId")]
        public string FamilyId { get; set; }

        [JsonProperty("userEmail")]
        public string UserEmail { get; set; }

        [JsonProperty("predictionJson")]
        public string PredictionJson { get; set; }

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }
    }
}