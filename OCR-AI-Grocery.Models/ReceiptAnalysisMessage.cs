using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.Models
{
    public class ReceiptAnalysisMessage
    {
        [JsonProperty("familyId")]
        public string FamilyId { get; set; }

        [JsonProperty("userEmail")]
        public string UserEmail { get; set; }
    }
}
