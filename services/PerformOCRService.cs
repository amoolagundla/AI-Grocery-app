using Azure.AI.Vision.ImageAnalysis;
using Azure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OCR_AI_Grocery.services
{
    public  class PerformOCRService
    {
        public async Task<string> PerformOCR(Stream imageStream,string _visionKey,string _visionEndpoint,ILogger logger)
        {
            try
            {
                if (string.IsNullOrEmpty(_visionKey) || string.IsNullOrEmpty(_visionEndpoint))
                {
                    throw new InvalidOperationException("Vision API credentials are not properly configured");
                }

                var credential = new AzureKeyCredential(_visionKey);
                var client = new ImageAnalysisClient(new Uri(_visionEndpoint), credential);
                var result = await client.AnalyzeAsync(BinaryData.FromStream(imageStream), VisualFeatures.Read);

                if (result.Value?.Read?.Blocks == null)
                {
                    return "No text found.";
                }

                return result.Value.Read.Blocks
                    .SelectMany(b => b.Lines)
                    .Select(l => l.Text)
                    .DefaultIfEmpty("No text found.")
                    .Aggregate((current, next) => $"{current} {next}");
            }
            catch (Exception ex)
            {
                logger.LogError($"OCR Error: {ex.Message}");
                return "OCR Failed.";
            }
        }
    }
}
