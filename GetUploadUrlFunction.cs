using System;
using System.Threading.Tasks;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Azure.Storage.Blobs.Models;

public class GetUploadUrlFunction
{
    private readonly ILogger<GetUploadUrlFunction> _logger;

    public GetUploadUrlFunction(ILogger<GetUploadUrlFunction> logger)
    {
        _logger = logger;
    }

    [Function(nameof(GetUploadUrlFunction))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequestData req,
        FunctionContext context)
    {
        _logger.LogInformation("Generating a pre-signed upload URL using SAS Token.");

        string storageAccountName = "reciepts"; // Make sure this is correct
        string containerName = "receipts";
        string fileName = $"receipt_{Guid.NewGuid()}.jpg"; // Unique filename

        try
        {
            // Authenticate using Managed Identity
            var blobServiceClient = new BlobServiceClient(
                new Uri($"https://{storageAccountName}.blob.core.windows.net"),
                new DefaultAzureCredential());

            var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = blobContainerClient.GetBlobClient(fileName);

            // Get User Delegation Key
            var userDelegationKey = await blobServiceClient.GetUserDelegationKeyAsync(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1));

            // Generate SAS Token
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = fileName,
                Resource = "b",
                ExpiresOn = DateTime.UtcNow.AddMinutes(15) // URL valid for 15 mins
            };
            sasBuilder.SetPermissions(BlobContainerSasPermissions.Write);

            var sasToken = sasBuilder.ToSasQueryParameters(userDelegationKey, storageAccountName).ToString();

            var sasUrl = $"{blobClient.Uri}?{sasToken}";

            return new OkObjectResult(new { uploadUrl = sasUrl, fileName });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error generating upload URL: {ex.Message}");
            return new ObjectResult("Error generating upload URL") { StatusCode = 500 };
        }
    }
}
