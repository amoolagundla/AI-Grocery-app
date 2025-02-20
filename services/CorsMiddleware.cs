using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using System;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.services
{
    public class CorsMiddleware : IFunctionsWorkerMiddleware
    {
        public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
        {
            await next(context); // No need to assign to a variable

            var httpResponseData = context.GetHttpResponseData();
            if (httpResponseData != null)
            {
                var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                if (env == "Development")
                {
                    httpResponseData.Headers.Add("Access-Control-Allow-Origin", "http://localhost:4200");
                    httpResponseData.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                    httpResponseData.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
                }
            }
        }
    }
}
