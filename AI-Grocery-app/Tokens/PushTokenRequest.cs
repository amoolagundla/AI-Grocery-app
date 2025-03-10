using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.Tokens
{
    public class PushTokenRequest
    {
        public string UserEmail { get; set; }
        public string Token { get; set; }
        public string Platform { get; set; }
    }
}
