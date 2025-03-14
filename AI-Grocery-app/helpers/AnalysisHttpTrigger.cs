using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs; 
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCR_AI_Grocey.Services.Interfaces;
using System.Collections.Generic;
using Microsoft.Azure.Functions.Worker;
using OCR_AI_Grocery.Models;

namespace OCR_AI_Grocey.Functions
{
    public class AnalysisHttpTrigger
    {
        private readonly IAnalysisQueue _analysisQueue;

        public AnalysisHttpTrigger(IAnalysisQueue analysisQueue)
        {
            _analysisQueue = analysisQueue;
        }
       
        [Function("ProcessReceiptAnalysis")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req )
        { 

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonConvert.DeserializeObject<ReceiptAnalysisRequest>(requestBody);

                

                // Process metadata
                var metadata = new Dictionary<string, string>();

                if (!string.IsNullOrEmpty(data.UserEmail))
                {
                    metadata.Add("email", data.UserEmail);
                }

                if (!string.IsNullOrEmpty(data.FamilyId))
                {
                    metadata.Add("familyId", data.FamilyId);
                }

                // Send to analysis queue
                await _analysisQueue.SendToAnalysisQueue(metadata, "");

                return new OkObjectResult(new
                {
                    message = "Receipt analysis request successfully queued",
                    requestId = Guid.NewGuid().ToString()
                });
            }
            catch (Exception ex)
            {
                
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }

    
}