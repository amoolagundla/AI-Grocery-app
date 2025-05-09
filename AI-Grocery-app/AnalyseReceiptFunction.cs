using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OCR_AI_Grocey.Services.Interfaces;

namespace OCR_AI_Grocery
{
    public class AnalyseReceiptFunction
    {
        private readonly IAnalyzeUserReceiptsService _analyzeUserReceiptsService;
        private readonly ILogger<AnalyseReceiptFunction> _logger; 
        public AnalyseReceiptFunction(
            IAnalyzeUserReceiptsService analyzeUserReceiptsService,
            ILoggerFactory loggerFactory)
        {
            _analyzeUserReceiptsService = analyzeUserReceiptsService;
            _logger = loggerFactory.CreateLogger<AnalyseReceiptFunction>();
        }

        [Function("AnalyseReceiptFunction")]
        public async Task<object> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "AnalyseReceipt/{message}")] HttpRequestData req,
            string message)
        {
            try
            {
                 await _analyzeUserReceiptsService.UpdateShoppingLists(new Models.ReceiptAnalysisMessage()
                {
                     FamilyId= "b4c3a552-fa79-48ce-ad26-fd2c93375e4f",
                      UserEmail="am8215@gmail.com"
                });
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AnalyzeUserReceiptsActivityFunction");
                throw;
            }
        }
    }
}
