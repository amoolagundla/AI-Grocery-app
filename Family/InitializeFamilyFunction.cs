using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCR_AI_Grocery.Family.models; 
using OCR_AI_Grocery.Services.Repositories; 

namespace OCR_AI_Grocery
{
    public class InitializeFamilyFunction
    {
        private readonly ILogger _logger;
        private readonly IFamilyRepository _familyRepository;

        public InitializeFamilyFunction(
            ILoggerFactory loggerFactory,
            IFamilyRepository familyRepository)
        {
            _logger = loggerFactory.CreateLogger<InitializeFamilyFunction>();
            _familyRepository = familyRepository;
        }

        [Function("InitializeFamily")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/initialize")] HttpRequest req)
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonConvert.DeserializeObject<InitializeFamilyRequest>(requestBody);

                if (string.IsNullOrEmpty(data?.Email))
                {
                    return new BadRequestObjectResult(new { message = "Email is required." });
                }

                _logger.LogInformation($"Initializing family for user: {data.Email}");

                // Use the repository to initialize the family
                var result = await _familyRepository.InitializeFamily(data.Email);

                _logger.LogInformation(result.IsNew
                    ? $"Created new family with ID {result.FamilyId}"
                    : $"Using existing family with ID {result.FamilyId}");

                return new OkObjectResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initializing family: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}