using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OCR_AI_Grocery.Services.Repositories;
using System;
using System.Threading.Tasks;

namespace OCR_AI_Grocery
{
    public class GetFamilyInvitesFunction
    {
        private readonly ILogger _logger;
        private readonly IFamilyRepository _familyRepository;

        public GetFamilyInvitesFunction(
            ILoggerFactory loggerFactory,
            IFamilyRepository familyRepository)
        {
            _logger = loggerFactory.CreateLogger<GetFamilyInvitesFunction>();
            _familyRepository = familyRepository;
        }

        [Function("GetFamilyInvites")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "family/invites/{email}")] HttpRequest req,
            string email)
        {
            try
            {
                _logger.LogInformation($"Fetching invites for email: {email}");

                var invites = await _familyRepository.GetPendingInvitesByEmail(email);

                _logger.LogInformation($"Found {invites.Count} pending invites for {email}");
                return new OkObjectResult(invites);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching invites: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}