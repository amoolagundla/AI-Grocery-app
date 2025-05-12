using OCR_AI_Grocery.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Interfaces
{
    public interface IAIMLInterface
    {
        Task SendNotification(string message);
    }
}
