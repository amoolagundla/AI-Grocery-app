using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Interfaces
{
    public interface IReceiptProcessingService
    {
        Task ProcessReceiptEvents(string[] events);
        Task ProcessSingleEvent(string eventJson);
    }
}
