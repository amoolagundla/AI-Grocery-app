
using System.Text.Json.Serialization;
namespace OCR_AI_Grocery.Family.models
{

    namespace OCR_AI_Grocery.models
    {
        public class CreateFamilyRequest
        {
            [JsonPropertyName("email")]
            public string? Email { get; set; }

            [JsonPropertyName("familyName")]
            public string? FamilyName { get; set; } // Optional, will use default if not provided
        }
    }
}
