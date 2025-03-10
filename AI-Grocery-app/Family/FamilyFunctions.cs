using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCR_AI_Grocery.Family.models;
using OCR_AI_Grocery.models;
using OCR_AI_Grocery.Services.Repositories;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; 

namespace OCR_AI_Grocery
{
    public class FamilyFunctions
    {
        private readonly ILogger _logger;
        private readonly IFamilyRepository _familyRepository;

        public FamilyFunctions(
            ILoggerFactory loggerFactory,
            IFamilyRepository familyRepository)
        {
            _logger = loggerFactory.CreateLogger<FamilyFunctions>();
            _familyRepository = familyRepository;
        }

        // GET /api/family/byEmail/{email}
        [Function("GetFamiliesByEmail")]
        public async Task<IActionResult> GetFamiliesByEmail(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "family/byEmail/{email}")] HttpRequest req,
            string email)
        {
            try
            {
                _logger.LogInformation($"Fetching families for email: {email}");

                var families = await _familyRepository.GetFamiliesByEmail(email);

                return new OkObjectResult(families);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching families: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        // POST /api/family/{familyId}/inviteMember
        [Function("InviteFamilyMember")]
        public async Task<IActionResult> InviteFamilyMember(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/{familyId}/inviteMember")] HttpRequest req,
            string familyId)
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonConvert.DeserializeObject<dynamic>(requestBody);

                string invitedUserEmail = data?.email?.ToString().ToLower() ?? string.Empty;
                string invitedBy = data?.invitedBy?.ToString().ToLower() ?? string.Empty;

                if (string.IsNullOrEmpty(invitedUserEmail) || string.IsNullOrEmpty(invitedBy))
                {
                    return new BadRequestObjectResult(new { message = "Email and inviter details are required." });
                }

                // Check for existing pending invite
                bool hasPendingInvite = await _familyRepository.CheckForPendingInvite(invitedUserEmail, familyId);

                if (hasPendingInvite)
                {
                    return new ConflictObjectResult(new { message = "Pending invitation already exists." });
                }

                var invite = new FamilyInvite
                {
                    InviteId = Guid.NewGuid().ToString(),
                    FamilyId = familyId,
                    InvitedUserEmail = invitedUserEmail,
                    InvitedBy = invitedBy,
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                };

                await _familyRepository.CreateFamilyInvite(invite);

                return new OkObjectResult(new { message = "Invitation sent successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending invite: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [Function("ProcessFamilyInvite")]
        public async Task<IActionResult> ProcessInvite(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/invites/{inviteId}/{invitedUserEmail}/process")] HttpRequest req,
            string inviteId, string invitedUserEmail)
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonConvert.DeserializeObject<dynamic>(requestBody);
                string action = data?.action?.ToString().ToLower() ?? "";

                if (string.IsNullOrEmpty(action) || (action != "accept" && action != "reject"))
                {
                    return new BadRequestObjectResult(new { message = "Invalid action. Must be 'accept' or 'reject'." });
                }

                // Find the invite
                var invites = await _familyRepository.GetPendingInvites(inviteId, invitedUserEmail);

                if (!invites.Any())
                {
                    return new NotFoundObjectResult(new { message = "Invite not found." });
                }

                var invite = invites.First();

                if (action == "accept")
                {
                    // Check if junction already exists
                    bool junctionExists = await _familyRepository.CheckFamilyJunctionExists(
                        invite.InvitedUserEmail, invite.FamilyId);

                    if (!junctionExists)
                    {
                        var junction = new FamilyJunction
                        {
                            Id = Guid.NewGuid().ToString(),
                            Email = invite.InvitedUserEmail,
                            FamilyId = invite.FamilyId,
                            JoinDate = DateTime.UtcNow,
                            Status = "Active",
                            PartitionKey = invite.FamilyId  // Make sure this matches your model
                        };

                        await _familyRepository.CreateFamilyJunction(junction);
                    }
                }

                // Update the invite
                invite.Status = action == "accept" ? "accepted" : "rejected";
                invite.ResponseDate = DateTime.UtcNow;

                await _familyRepository.UpdateFamilyInvite(invite);

                return new OkObjectResult(new
                {
                    message = $"Invitation {action}ed successfully.",
                    familyId = invite.FamilyId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing invite: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [Function("GetFamilyDetails")]
        public async Task<IActionResult> GetFamilyDetails(
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "family/{id}")] HttpRequest req,
           string id)
        {
            try
            {
                _logger.LogInformation($"Fetching details for family: {id}");

                var family = await _familyRepository.GetFamilyById(id);

                if (family == null)
                {
                    return new NotFoundObjectResult($"Family with ID {id} not found");
                }

                return new OkObjectResult(family);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving family: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}