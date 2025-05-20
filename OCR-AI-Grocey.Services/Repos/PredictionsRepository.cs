using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using OCR_AI_Grocery.Models;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Azure.Cosmos.Linq;

namespace OCR_AI_Grocey.Services.Repos
{
    public class PredictionsRepository : IPredictionsRepository
    {
        private readonly Container _container;
        private readonly ILogger<PredictionsRepository> _logger;

        public PredictionsRepository(CosmosClient cosmosClient, ILoggerFactory loggerFactory)
        {
            _container = cosmosClient.GetContainer("ReceiptsDB", "predictions");
            _logger = loggerFactory.CreateLogger<PredictionsRepository>();
        }

        public async Task SavePrediction(PredictionDocument prediction)
        {
            await _container.UpsertItemAsync(prediction, new PartitionKey(prediction.UserEmail));
            _logger.LogInformation($"Saved prediction for family {prediction.UserEmail}");
        }

        public async Task<PredictionDocument?> GetLatestPredictionByUserEmail(string userEmail)
        {
            var query = _container.GetItemLinqQueryable<PredictionDocument>(true)
                .Where(p => p.UserEmail == userEmail)
                .OrderByDescending(p => p.CreatedAt)
                .Take(1)
                .ToFeedIterator();

            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();
                return response.FirstOrDefault();
            }
            return null;
        }
    }
}