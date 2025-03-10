using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.Models
{
    public class EventGridData
    {
        [JsonPropertyName("api")]
        public string Api { get; set; }

        [JsonPropertyName("clientRequestId")]
        public string ClientRequestId { get; set; }

        [JsonPropertyName("requestId")]
        public string RequestId { get; set; }

        [JsonPropertyName("eTag")]
        public string ETag { get; set; }

        [JsonPropertyName("contentType")]
        public string ContentType { get; set; }

        [JsonPropertyName("contentLength")]
        public int ContentLength { get; set; }

        [JsonPropertyName("blobType")]
        public string BlobType { get; set; }

        [JsonPropertyName("accessTier")]
        public string AccessTier { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("sequencer")]
        public string Sequencer { get; set; }

        [JsonPropertyName("storageDiagnostics")]
        public StorageDiagnostics StorageDiagnostics { get; set; }
    }

    public class StorageDiagnostics
    {
        [JsonPropertyName("batchId")]
        public string BatchId { get; set; }
    }
}
