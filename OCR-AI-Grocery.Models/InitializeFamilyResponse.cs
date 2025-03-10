using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.Models
{
    public class InitializeFamilyRequest
    {
        [JsonProperty("email")]
        public string Email { get; set; }
    }

    public class InitializeFamilyResponse
    {
        [JsonProperty("familyId")]
        public string FamilyId { get; set; }

        [JsonProperty("isNew")]
        public bool IsNew { get; set; }
    }
}
