using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using OCR_AI_Grocery.Models;
using OCR_AI_Grocey.Services.Helpers;
using Microsoft.Azure.Cosmos.Linq;
using OCR_AI_Grocey.Services.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Azure.Functions.Worker.Http;

namespace OCR_AI_Grocery
{
    public class TrainTimeGen1Function
    {
        
        private readonly ILogger<TrainTimeGen1Function> _logger;
        private readonly ITimeGen1Interface timeGen;

        public TrainTimeGen1Function(ILogger<TrainTimeGen1Function> logger, ITimeGen1Interface timeGen)
        {
            _logger = logger;
            this.timeGen = timeGen;
        }

        [Function("TrainTimeGen1HttpFunction")]
        public string Run(
              [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req,
              FunctionContext context)
        {
           // await timeGen.TrainTheModel();
            return "OK";
        }
         
    }
}