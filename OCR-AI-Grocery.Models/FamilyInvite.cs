using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.Family.models
{
    public class FamilyInvite
    {
        [JsonProperty("id")]
        public string InviteId { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("familyId")]
        public string FamilyId { get; set; } = string.Empty;

        [JsonProperty("invitedUserEmail")]
        public string InvitedUserEmail { get; set; } = string.Empty;

        [JsonProperty("invitedBy")]
        public string InvitedBy { get; set; } = string.Empty;

        [JsonProperty("status")]
        public string Status { get; set; } = "pending";

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonProperty("responseDate")]
        public DateTime? ResponseDate { get; set; }
    }
}
