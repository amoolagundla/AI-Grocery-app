using Azure.Identity;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Net;

public class GetUploadUrlFunction
{
    private readonly ILogger<GetUploadUrlFunction> _logger;

    public GetUploadUrlFunction(ILogger<GetUploadUrlFunction> logger)
    {
        _logger = logger;
    }

    [Function(nameof(GetUploadUrlFunction))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequestData req,
        FunctionContext context)
    {
        _logger.LogInformation("Generating a pre-signed upload URL using SAS Token.");

        // Configuration
        string storageAccountName = "reciepts";
        string containerName = "receipts";
        string fileName = $"receipt_{Guid.NewGuid()}.jpg"; // Unique filename

        try
        {
            // Authenticate using Managed Identity
            // Make sure your function has Identity enabled in Azure portal
            var blobServiceClient = new BlobServiceClient(
                new Uri($"https://{storageAccountName}.blob.core.windows.net"),
                new DefaultAzureCredential());

            // Get container client
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);

            // Check if container exists, create if not
            await blobContainerClient.CreateIfNotExistsAsync(PublicAccessType.None);

            var blobClient = blobContainerClient.GetBlobClient(fileName);

            // Get User Delegation Key (Valid for 2 hours)
            var userDelegationKey = await blobServiceClient.GetUserDelegationKeyAsync(
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddHours(2));

            // Generate SAS Token
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = fileName,
                Resource = "b", // 'b' for blob
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5), // Ensures validity
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(6)    // 2-hour expiration
            };

            // Add permissions for file uploads
            sasBuilder.SetPermissions(BlobSasPermissions.All);



            // Generate SAS Token
            var sasToken = sasBuilder.ToSasQueryParameters(userDelegationKey, storageAccountName).ToString();
            var sasUrl = $"{blobClient.Uri}?{sasToken}";

            // Create the response
            var response = req.CreateResponse(HttpStatusCode.OK); 

            await response.WriteAsJsonAsync(new
            {
                uploadUrl = sasUrl,
                fileName = fileName,
                expiresAt = DateTimeOffset.UtcNow.AddHours(2)
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error generating upload URL: {ex.Message}");
            _logger.LogDebug($"Stack trace: {ex.StackTrace}");

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json");

            await errorResponse.WriteAsJsonAsync(new
            {
                error = "Error generating upload URL",
                message = ex.Message,
                timestamp = DateTimeOffset.UtcNow
            });

            return errorResponse;
        }
    }
}