using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.Family.models
{
    public class FamilyInviteRequest
    {
        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("invitedBy")]
        public string? InvitedBy { get; set; }
    }

    public class FamilyInviteResponse
    {
        [JsonPropertyName("inviteId")]
        public string InviteId { get; set; } = string.Empty;

        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty; // "accept" or "reject"
    }

    public class FamilyUpdateRequest
    {
        [JsonPropertyName("familyId")]
        public string? FamilyId { get; set; }

        [JsonPropertyName("familyName")]
        public string? FamilyName { get; set; }
    }

    public class FamilyMemberResponse
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("joinDate")]
        public DateTime JoinDate { get; set; }

        [JsonPropertyName("isPrimary")]
        public bool IsPrimary { get; set; }
    }
}
