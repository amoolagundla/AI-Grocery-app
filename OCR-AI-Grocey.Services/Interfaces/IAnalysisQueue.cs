using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Interfaces
{
    public interface IAnalysisQueue
    {
          Task SendToAnalysisQueue(IDictionary<string, string> metadata, string extractedText, string subject = "ReceiptAnalysis");
    }
}
