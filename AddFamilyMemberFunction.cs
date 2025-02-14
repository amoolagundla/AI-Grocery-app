using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCR_AI_Grocery.models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace OCR_AI_Grocery
{
    public class FamilyFunctions
    {
        private readonly CosmosClient _cosmosClient;
        private readonly Container _container;

        public FamilyFunctions()
        {
            string cosmosDbConnection = Environment.GetEnvironmentVariable("CosmosDBConnectionString") ?? string.Empty;
            _cosmosClient = new CosmosClient(cosmosDbConnection);
            _container = _cosmosClient.GetContainer("ReceiptsDB", "FamilyJunction");
        }

        [Function("AddFamilyMember")]
        public async Task<IActionResult> AddFamilyMember(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/{familyId}/addMember")] HttpRequest req,
            string familyId,
            ILogger log)
        {
            log.LogInformation($"Processing request to add a family member to Family ID: {familyId}");

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic? data = JsonConvert.DeserializeObject(requestBody);

                if (data == null || string.IsNullOrEmpty(familyId))
                {
                    return new BadRequestObjectResult(new { message = "Invalid request body or missing familyId." });
                }

                string memberId = data?.id ?? string.Empty;
                string email = data?.email ?? string.Empty;
                string name = data?.name ?? string.Empty;
                string photoURL = data?.photoURL ?? string.Empty;

                if (string.IsNullOrEmpty(memberId) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(name))
                {
                    return new BadRequestObjectResult(new { message = "Missing required fields: id, email, or name." });
                }

                var newMember = new
                {
                    FamilyId = familyId,
                    MemberId = memberId,
                    Email = email,
                    Name = name,
                    PhotoURL = photoURL,
                    CreatedAt = DateTime.UtcNow
                };

                log.LogInformation($"Successfully added family member: {JsonConvert.SerializeObject(newMember)}");

                return new OkObjectResult(new
                {
                    message = "Family member added successfully!",
                    familyId,
                    memberId,
                    name,
                    email,
                    photoURL
                });
            }
            catch (Exception ex)
            {
                log.LogError($"Error adding family member: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        [Function("GetFamilyByEmail")]
        public async Task<IActionResult> GetFamilyByEmail([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "family/get/{email}")] HttpRequest req,
                                                         string email,
                                                         ILogger log)
        {
            log.LogInformation($"Fetching family details for email: {email}");

            if (string.IsNullOrEmpty(email))
            {
                return new BadRequestObjectResult(new { message = "Email parameter is required." });
            }

            try
            {
                var query = new QueryDefinition("SELECT * FROM c WHERE c.primaryEmail = @email")
                            .WithParameter("@email", email);

                using FeedIterator<FamilyEntity> resultSet = _container.GetItemQueryIterator<FamilyEntity>(query);

                if (resultSet.HasMoreResults)
                {
                    var response = await resultSet.ReadNextAsync();
                    var family = response.FirstOrDefault();

                    if (family == null)
                    {
                        return new NotFoundObjectResult(new { message = "Family not found." });
                    }

                    return new OkObjectResult(new
                    {
                        FamilyId = family.Id,
                        FamilyName = family.FamilyName,
                        PrimaryEmail = family.PrimaryEmail
                    });
                }

                return new NotFoundObjectResult(new { message = "Family not found." });
            }
            catch (Exception ex)
            {
                log.LogError($"Error retrieving family details: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }

}
