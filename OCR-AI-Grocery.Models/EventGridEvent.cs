using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.Models
{
    public class EventGridEvent
    {
        [JsonPropertyName("topic")]
        public string Topic { get; set; }

        [JsonPropertyName("subject")]
        public string Subject { get; set; }

        [JsonPropertyName("eventType")]
        public string EventType { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("data")]
        public EventGridData Data { get; set; }

        [JsonPropertyName("dataVersion")]
        public string DataVersion { get; set; }

        [JsonPropertyName("metadataVersion")]
        public string MetadataVersion { get; set; }

        [JsonPropertyName("eventTime")]
        public DateTime EventTime { get; set; }
    }
}
