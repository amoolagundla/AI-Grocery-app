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

        string storageAccountName = "reciepts"; // Verify this is the correct account name
        string containerName = "receipts";
        string fileName = $"receipt_{Guid.NewGuid()}.jpg"; // Unique filename

        try
        {
            // Authenticate using Managed Identity (Make sure your function has Identity enabled in Azure)
            var blobServiceClient = new BlobServiceClient(
                new Uri($"https://{storageAccountName}.blob.core.windows.net"),
                new DefaultAzureCredential());

            var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = blobContainerClient.GetBlobClient(fileName);

            // Get User Delegation Key (Valid for 2 hours)
            var userDelegationKey = await blobServiceClient.GetUserDelegationKeyAsync(
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddHours(2));

            // Generate SAS Token (Fixed blob name issue)
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = fileName,
                Resource = "b",
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5), // Ensures validity
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(2)    // 2-hour expiration
            };

            // ✅ Add `Create` and `Write` permissions for file uploads
            sasBuilder.SetPermissions(BlobSasPermissions.Read | BlobSasPermissions.Write | BlobSasPermissions.Create);


            // Generate SAS Token
            var sasToken = sasBuilder.ToSasQueryParameters(userDelegationKey, storageAccountName).ToString();
            var sasUrl = $"{blobClient.Uri}?{sasToken}";

            return new OkObjectResult(new { uploadUrl = sasUrl, fileName });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error generating upload URL: {ex.Message}");
            return new ObjectResult(new { error = "Error generating upload URL" }) { StatusCode = 500 };
        }
    }
}
