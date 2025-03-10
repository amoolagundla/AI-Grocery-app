using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.models
{
    public class Data
    {
        public string api { get; set; } = string.Empty;
        public string clientRequestId { get; set; } = string.Empty;
        public string requestId { get; set; } = string.Empty;
        public string eTag { get; set; } = string.Empty;
        public string contentType { get; set; } = string.Empty;
        public int contentLength { get; set; }
        public string blobType { get; set; } = string.Empty;
        public string accessTier { get; set; } = string.Empty;
        public string url { get; set; } = string.Empty;
        public string sequencer { get; set; } = string.Empty;
        public StorageDiagnostics? storageDiagnostics { get; set; }
    }
}
