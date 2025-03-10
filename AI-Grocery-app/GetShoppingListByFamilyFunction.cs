using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OCR_AI_Grocey.Services.Interfaces;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.Functions
{
    public class ShoppingListFunctions
    {
        private readonly ILogger _logger;
        private readonly IShoppingListRepository _shoppingListRepository;

        public ShoppingListFunctions(
            ILoggerFactory loggerFactory,
            IShoppingListRepository shoppingListRepository)
        {
            _logger = loggerFactory.CreateLogger<ShoppingListFunctions>();
            _shoppingListRepository = shoppingListRepository;
        }

        [Function("GetShoppingListsForFamily")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "family/{userEmail}/shoppingLists")] HttpRequestData req,
            string userEmail)
        {
            _logger.LogInformation($"Fetching shopping lists for family ID: {userEmail}");

            try
            {
                // Use the repository to get shopping lists
                var shoppingLists = await _shoppingListRepository.GetShoppingListsForFamily(userEmail);

                // Create the appropriate response
                HttpResponseData response;
                if (shoppingLists == null || !shoppingLists.Any())
                {
                    _logger.LogInformation($"No shopping lists found for family: {userEmail}");
                    response = req.CreateResponse(HttpStatusCode.NotFound);
                    await response.WriteAsJsonAsync(new { message = "No shopping lists found for this family." });
                }
                else
                {
                    _logger.LogInformation($"Retrieved {shoppingLists.Count} shopping lists for family: {userEmail}");
                    response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(shoppingLists);
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching shopping lists: {ex.Message}");

                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new
                {
                    message = "Error retrieving shopping lists",
                    error = ex.Message
                });
                return errorResponse;
            }
        }
    }
}