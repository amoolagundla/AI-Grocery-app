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
using System.Net;

namespace OCR_AI_Grocery
{
    public class TrainTimeGen1Function
    {
        private readonly ILogger<TrainTimeGen1Function> _logger;
        private readonly ITimeGen1Interface timeGen;
        private readonly IPredictionsRepository _predictionsRepository;

        public TrainTimeGen1Function(ILogger<TrainTimeGen1Function> logger, ITimeGen1Interface timeGen, IPredictionsRepository predictionsRepository)
        {
            _logger = logger;
            this.timeGen = timeGen;
            _predictionsRepository = predictionsRepository;
        }

        [Function("TrainTimeGen1HttpFunction")]
        public string Run(
              [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req,
              FunctionContext context)
        {
           // await timeGen.TrainTheModel();
            return "OK";
        }

        [Function("GetPredictionsByUserEmail")]
        public async Task<HttpResponseData> GetPredictionsByUserEmail(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "predictions/{userEmail}")] HttpRequestData req,
            string userEmail)
        {
            var prediction = await _predictionsRepository.GetLatestPredictionByUserEmail(userEmail);

            var response = req.CreateResponse(prediction != null ? HttpStatusCode.OK : HttpStatusCode.NotFound);

            if (prediction != null)
            {
                // Optionally, parse prediction.PredictionJson to return as an object
                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(prediction.PredictionJson);
            }
            else
            {
                await response.WriteStringAsync("{\"message\": \"No prediction found for this user.\"}");
            }

            return response;
        }
    }
}