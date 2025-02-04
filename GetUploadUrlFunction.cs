using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc; 
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Azure.Functions.Worker;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker.Http;

namespace OCR_AI_Grocery
{
    public class GetUploadUrlFunction
    {
        private readonly ILogger<GetUploadUrlFunction> _logger;

        public GetUploadUrlFunction(ILogger<GetUploadUrlFunction> logger)
        {
            _logger = logger;
        }

        [Function(nameof(GetUploadUrlFunction))]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequestData req,
            FunctionContext context)
        {
            _logger.LogInformation("Generating a pre-signed upload URL using Managed Identity.");

            string storageAccountName = "receipts"; // Replace with actual storage account name
            string containerName = "receipts";
            string fileName = $"receipt_{Guid.NewGuid()}.jpg"; // Unique filename

            try
            {
                // Authenticate using Managed Identity (No Connection String Needed)
                var blobUri = new Uri($"https://{storageAccountName}.blob.core.windows.net");
                var blobServiceClient = new BlobServiceClient(blobUri, new DefaultAzureCredential(new DefaultAzureCredentialOptions()
                {
                    ManagedIdentityClientId= "3bcd9401-d9d8-4514-9229-6e33a4ba17bc"
                }));
                var blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = blobContainerClient.GetBlobClient(fileName);

                // Generate a SAS Token (valid for 15 minutes)
                var sasBuilder = new BlobSasBuilder
                {
                    BlobContainerName = containerName,
                    BlobName = fileName,
                    Resource = "b",
                    ExpiresOn = DateTime.UtcNow.AddMinutes(15) // URL expires in 15 minutes
                };
                sasBuilder.SetPermissions(BlobContainerSasPermissions.Write);

                var sasUri = blobClient.GenerateSasUri(sasBuilder);

                return new OkObjectResult(new { uploadUrl = sasUri.ToString(), fileName });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating upload URL: {ex.Message}");
                return new ObjectResult("Error generating upload URL") { StatusCode = 500 };
            }
        }
    }
}
