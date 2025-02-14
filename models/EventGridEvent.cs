using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.models
{
    public class EventGridEvent
    {
        public string topic { get; set; } = string.Empty;
        public string subject { get; set; } = string.Empty;
        public string eventType { get; set; } = string.Empty;
        public string id { get; set; } = string.Empty;
        public Data? data { get; set; }
        public string dataVersion { get; set; } = string.Empty;
        public string metadataVersion { get; set; } = string.Empty;
        public DateTime eventTime { get; set; }
    }
}
