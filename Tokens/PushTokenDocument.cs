using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.Tokens
{

    public class PushTokenDocument
    {
        public string id { get; set; }
        public string UserEmail { get; set; }  // This is our partition key
        public string Token { get; set; }
        public string Platform { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
