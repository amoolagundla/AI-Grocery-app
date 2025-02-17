using global::OCR_AI_Grocery.models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCR_AI_Grocery.Family.models;
using OCR_AI_Grocery.Family.models.OCR_AI_Grocery.models;
using OCR_AI_Grocery.models;
using System;
using System.Threading.Tasks;

namespace OCR_AI_Grocery.Family
{ 
        public class FamilyFunctions
        {
            private readonly ILogger _logger;
            private readonly CosmosClient _cosmosClient;
            private readonly Container _familyContainer;
            private readonly Container _familyJunctionContainer;

            public FamilyFunctions(ILoggerFactory loggerFactory)
            {
                _logger = loggerFactory.CreateLogger<FamilyFunctions>();
                string cosmosDbConnection = Environment.GetEnvironmentVariable("CosmosDBConnectionString") ?? string.Empty;
                _cosmosClient = new CosmosClient(cosmosDbConnection);

                _familyContainer = _cosmosClient.GetContainer("ReceiptsDB", "Families");
                _familyJunctionContainer = _cosmosClient.GetContainer("ReceiptsDB", "FamilyJunction");
            }

            [Function("CreateFamily")]
            public async Task<IActionResult> CreateFamily(
                [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "family/create")] HttpRequest req)
            {
                try
                {
                    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                    var data = JsonConvert.DeserializeObject<CreateFamilyRequest>(requestBody);

                    if (data?.Email == null)
                    {
                        return new BadRequestObjectResult(new { message = "Email is required." });
                    }

                    // Create new family
                    var familyId = Guid.NewGuid().ToString();
                    var family = new FamilyEntity
                    {
                        Id = familyId,
                        FamilyName = $"{data.Email.Split('@')[0]}'s Family", // Default name
                        PrimaryEmail = data.Email.ToLower()
                    };

                    await _familyContainer.CreateItemAsync(family, new PartitionKey(familyId));

                    // Create family junction for the creator
                    var junction = new FamilyJunction
                    {
                        Id = data.Email.ToLower(),
                        Email = data.Email.ToLower(),
                        FamilyId = familyId
                    };

                    await _familyJunctionContainer.CreateItemAsync(junction, new PartitionKey(familyId));

                    _logger.LogInformation($"Created new family with ID {familyId} for {data.Email}");

                    return new OkObjectResult(new
                    {
                        familyId = familyId,
                        message = "Family created successfully"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error creating family: {ex.Message}");
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }
            }

            [Function("GetFamilyByEmail")]
            public async Task<IActionResult> GetFamilyByEmail(
                [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "family/byEmail/{email}")] HttpRequest req,
                string email)
            {
                try
                {
                    var query = new QueryDefinition(
                        "SELECT * FROM c WHERE c.Email = @email")
                        .WithParameter("@email", email.ToLower());

                    using var iterator = _familyJunctionContainer.GetItemQueryIterator<FamilyJunction>(query);
                    var response = await iterator.ReadNextAsync();

                    if (response.Count == 0)
                    {
                        return new NotFoundObjectResult(new { message = "No family found for this email." });
                    }

                    var familyJunction = response.FirstOrDefault();

                    // Get family details
                    try
                    {
                        var family = await _familyContainer.ReadItemAsync<FamilyEntity>(
                            familyJunction.FamilyId,
                            new PartitionKey(familyJunction.FamilyId)
                        );

                        return new OkObjectResult(new
                        {
                            familyId = family.Resource.Id,
                            familyName = family.Resource.FamilyName,
                            primaryEmail = family.Resource.PrimaryEmail,
                            memberEmail = familyJunction.Email
                        });
                    }
                    catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return new NotFoundObjectResult(new { message = "Family details not found." });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error getting family: {ex.Message}");
                    return new StatusCodeResult(StatusCodes.Status500InternalServerError);
                }
            }
        }
    } 
