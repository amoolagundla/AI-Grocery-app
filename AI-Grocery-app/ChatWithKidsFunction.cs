using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using OCR_AI_Grocey.Services.Interfaces;

namespace OCR_AI_Grocery
{
    public class ChatWithKidsFunction
    {
        private readonly IOpenAIService _openAIService;
        private readonly ILogger<ChatWithKidsFunction> _logger;

        public ChatWithKidsFunction(IOpenAIService openAIService, ILogger<ChatWithKidsFunction> logger)
        {
            _openAIService = openAIService;
            _logger = logger;
        }

        [Function("ChatWithKids")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "chat/kids")] HttpRequestData req)
        {
            var requestBody = await req.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Request body is empty.");
                return badResponse;
            }

            var reply = await _openAIService.ChatWithKidsAsync(requestBody);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(reply);
            return response;
        }
    }
}