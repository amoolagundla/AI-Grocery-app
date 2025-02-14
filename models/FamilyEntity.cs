using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.models
{
    public class FamilyEntity
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("familyName")]
        public string FamilyName { get; set; }

        [JsonProperty("primaryEmail")]
        public string PrimaryEmail { get; set; }
    }
}
