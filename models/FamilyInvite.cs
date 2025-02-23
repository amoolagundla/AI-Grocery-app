using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.models
{
    public class FamilyInvite
    {
        [JsonProperty("id")]
        public string InvitedId { get; set; }

        [JsonProperty("partitionKey")]
        public string PartitionKey { get; set; }  // This should match InvitedUserEmail

        public string FamilyId { get; set; }
        public string InvitedUserEmail { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResponseDate { get; set; }
    }

    
}
