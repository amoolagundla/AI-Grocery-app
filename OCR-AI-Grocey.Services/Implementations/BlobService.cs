using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using OCR_AI_Grocey.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Implementations
{
    public class BlobService : IBlobService
    {
        private readonly ILogger<BlobService> _logger;

        public BlobService(ILogger<BlobService> logger)
        {
            _logger = logger;
        }

        public async Task<(Stream Content, IDictionary<string, string> Metadata)> DownloadBlobWithMetadataAsync(string blobUrl)
        {
            try
            {
                var blobClient = new BlobClient(new Uri(blobUrl), new DefaultAzureCredential());
                var response = await blobClient.DownloadStreamingAsync();
                Stream blobContent = response.Value.Content;

                var propertiesResponse = await blobClient.GetPropertiesAsync();
                var metadata = propertiesResponse.Value.Metadata;

                _logger.LogInformation($"✅ Successfully downloaded blob with {metadata.Count} metadata entries.");

                return (blobContent, metadata);
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error downloading blob: {ex.Message}");
                return (null, null);
            }
        }
    }
}
