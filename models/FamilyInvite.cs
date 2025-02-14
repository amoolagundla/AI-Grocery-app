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
        public string InviteId { get; set; } = string.Empty;

        public string FamilyId { get; set; } = string.Empty;
        public string InvitedUserEmail { get; set; } = string.Empty;
        public string Status { get; set; } = "pending"; // Default status is "pending"
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
