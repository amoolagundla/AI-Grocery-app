using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCR_AI_Grocery.Models.Receipt;
using OCR_AI_Grocey.Services.Interfaces;

namespace OCR_AI_Grocery
{
    public class ReceiptPostFunction
    {
        private readonly ILogger<ReceiptPostFunction> _logger;
        private readonly IReceiptService _receiptService;

        public ReceiptPostFunction(ILogger<ReceiptPostFunction> logger, IReceiptService receiptService)
        {
            _logger = logger;
            _receiptService = receiptService;
        }

        [Function("ReceiptPostFunction")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
        {
            _logger.LogInformation("Processing receipt data submission");

            try
            {
                // Read the request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

                if (string.IsNullOrEmpty(requestBody))
                {
                    _logger.LogWarning("Empty request body received");
                    return new BadRequestObjectResult("Please provide receipt data in the request body");
                }

                _logger.LogInformation($"Received data: {requestBody}");

                // Deserialize the request body to ReceiptDocument
                var receipt = JsonConvert.DeserializeObject<ReceiptDocument>(requestBody);

                if (receipt == null)
                {
                    _logger.LogWarning("Failed to deserialize receipt data");
                    return new BadRequestObjectResult("Invalid receipt data format");
                }

                // Ensure required fields are provided
                if (string.IsNullOrEmpty(receipt.FamilyId))
                {
                    _logger.LogWarning("Receipt missing FamilyId");
                    return new BadRequestObjectResult("FamilyId is required");
                }

                // Generate an ID if not provided
                if (string.IsNullOrEmpty(receipt.Id))
                {
                    receipt.Id = Guid.NewGuid().ToString();
                }

                // Set upload date if not provided
                if (receipt.UploadDate == default)
                {
                    receipt.UploadDate = DateTime.UtcNow;
                }

                // Save the receipt to Cosmos DB
                await _receiptService.SaveReceiptAsync(receipt);

                _logger.LogInformation($"Successfully saved receipt with ID: {receipt.Id}");

                // Return the saved receipt with its ID
                return new OkObjectResult(new
                {
                    message = "Receipt saved successfully",
                    receiptId = receipt.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing receipt: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");

                // Return a more detailed error in development, simpler in production
                return new ObjectResult(new
                {
                    error = "Failed to process receipt data",
                    details = ex.Message
                })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }
    }
}
