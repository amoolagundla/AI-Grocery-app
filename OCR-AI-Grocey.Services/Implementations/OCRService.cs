using Azure;
using Azure.AI.Vision.ImageAnalysis;
using Microsoft.Extensions.Logging;
using OCR_AI_Grocey.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCR_AI_Grocey.Services.Implementations
{
    public class OCRService : IOCRService
    {
        private readonly ILogger<OCRService> _logger;
        private const string VisionEndpoint = "https://reciept-vision.cognitiveservices.azure.com/";
        private const string VisionKey = "EzK1s1e1KwCa3ecEzzG8MnWk7caCsbd698URjSn9NltjqIOkfRQQJQQJ99BBACYeBjFXJ3w3AAAFACOGzB6B";

        public OCRService(ILogger<OCRService> logger)
        {
            _logger = logger;
        }

        public async Task<string> PerformOCR(Stream imageStream)
        {
            try
            {
                var credential = new AzureKeyCredential(VisionKey);
                var client = new ImageAnalysisClient(new Uri(VisionEndpoint), credential);
                var result = await client.AnalyzeAsync(BinaryData.FromStream(imageStream), VisualFeatures.Read);

                if (result.Value != null)
                {
                    return string.Join(" ", result.Value.Read.Blocks.SelectMany(b => b.Lines).Select(l => l.Text));
                }
                return "No text found.";
            }
            catch (Exception ex)
            {
                _logger.LogError($"OCR Error: {ex.Message}");
                return "OCR Failed.";
            }
        }
    }
}
