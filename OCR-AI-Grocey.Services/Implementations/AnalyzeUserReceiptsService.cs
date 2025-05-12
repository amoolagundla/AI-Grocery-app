using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCR_AI_Grocery.Models;
using OCR_AI_Grocery.Models.Receipt;
using OCR_AI_Grocey.Services.Helpers;
using OCR_AI_Grocey.Services.Interfaces;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace OCR_AI_Grocey.Services.Implementations
{
    public class AnalyzeUserReceiptsService : IAnalyzeUserReceiptsService
    {
        private readonly IReceiptRepository _receiptRepository;
        private readonly IShoppingListRepository _shoppingListRepository;
        private readonly IOpenAIService _openAIService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<AnalyzeUserReceiptsService> _logger;
        private readonly IAnalysisQueue analysisQueue;

        public AnalyzeUserReceiptsService(
            IReceiptRepository receiptRepository,
            IShoppingListRepository shoppingListRepository,
            IOpenAIService openAIService,
            INotificationService notificationService,
            ILoggerFactory loggerFactory,
            IAnalysisQueue analysisQueue)
        {
            _receiptRepository = receiptRepository;
            _shoppingListRepository = shoppingListRepository;
            _openAIService = openAIService;
            _notificationService = notificationService;
            _logger = loggerFactory.CreateLogger<AnalyzeUserReceiptsService>();
            this.analysisQueue = analysisQueue;
        }

        public async Task ProcessReceiptAnalysis(ServiceBusReceivedMessage message)
        {
            try
            {
                var receiptAnalysis = ValidateAndExtractMessage(message)
                    .ThrowIfMissingFamilyId();
                await UpdateShoppingLists(receiptAnalysis);

                _logger.LogInformation($"Successfully processed receipts for UserEmail {receiptAnalysis.UserEmail}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Receipt analysis failed");
                throw;
            }
        }

        public async Task UpdateShoppingLists(ReceiptAnalysisMessage receiptAnalysis)
        {
            var unprocessedReceipts = await FetchUnprocessedReceiptsForFamily(receiptAnalysis.FamilyId);

            if (!unprocessedReceipts.HasReceipts())
            {
                _logger.LogInformation($"No new receipts for UserEmail {receiptAnalysis.UserEmail}");
                return;
            }

            var shoppingListUpdate = await CreateShoppingListUpdate(unprocessedReceipts, receiptAnalysis.FamilyId);

            await Task.WhenAll(
                UpdateShoppingList(shoppingListUpdate),
                UpdateReceiptRecords(unprocessedReceipts, shoppingListUpdate),
                NotifyUser(receiptAnalysis, shoppingListUpdate) 
               // NotifyAIML(shoppingListUpdate.TimeSeriesData, receiptAnalysis.UserEmail)
            );
        }

        private async Task NotifyAIML(string timeSeriesData, string userEmail)
        {
            var dict= new Dictionary<string, string>();
            dict.Add("email", userEmail); 
            await analysisQueue.SendToAnalysisQueue(dict, JsonConvert.SerializeObject(timeSeriesData), "TimeGenAIML");
        }

        private ReceiptAnalysisMessage ValidateAndExtractMessage(ServiceBusReceivedMessage message)
        {
            return JsonConvert.DeserializeObject<ReceiptAnalysisMessage>(message.Body.ToString()) ?? new ReceiptAnalysisMessage();
        }

        private async Task<List<ReceiptDocument>> FetchUnprocessedReceiptsForFamily(string familyId)
        {
            try
            {
                var receipts = await _receiptRepository.FetchUnprocessedReceipts(familyId);
                _logger.LogInformation($"Found {receipts.Count} unprocessed receipts");
                return receipts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to fetch receipts for family {familyId}");
                throw;
            }
        }

        private async Task<ShoppingListUpdate> CreateShoppingListUpdate(
            List<ReceiptDocument> receipts,
            string familyId)
        {
            var shoppinglists= await _shoppingListRepository.GetExistingShoppingList(familyId);
            var items = await _openAIService.AnalyzeReceiptsWithOpenAI(receipts);
            var analyzedItems = items.Item1;
            var timeseriesData=items.Item2;

            if (analyzedItems == null || !analyzedItems.Any())
            {
                throw new InvalidOperationException("No items extracted from receipts");
            }

            var existingList = await _shoppingListRepository.GetExistingShoppingList(familyId);

            return new ShoppingListUpdate
            {
                ExistingList = existingList,
                NewItems = analyzedItems,
                StoreName = analyzedItems.Keys.FirstOrDefault() ?? "Unknown Store",
                MergedList = MergeShoppingLists(existingList, analyzedItems),
                TimeSeriesData= JsonConvert.SerializeObject(timeseriesData)
            };
        }

        private async Task UpdateShoppingList(ShoppingListUpdate update)
        {
            await _shoppingListRepository.UpdateShoppingList(update.MergedList);
            _logger.LogInformation($"Updated shopping list for family {update.MergedList.FamilyId}");
        }

        private async Task UpdateReceiptRecords(
            List<ReceiptDocument> receipts,
            ShoppingListUpdate update)
        {
            foreach (var receipt in receipts)
            {
                receipt.Processed = true;
                receipt.UploadDate = DateTime.UtcNow;
                receipt.StoreItems = update.NewItems;
                receipt.StoreName = update.StoreName;
                receipt.TimeSeriesData= update.TimeSeriesData;
                await _receiptRepository.UpdateReceipt(receipt);
            }
        }

        private async Task NotifyUser(
            ReceiptAnalysisMessage message,
            ShoppingListUpdate update)
        {
            await _notificationService.SendNotification(
                message,
                update.NewItems,
                update.MergedList,
                update.StoreName
            );
        }

        private ShoppingList MergeShoppingLists(
            ShoppingList existing,
            Dictionary<string, List<string>> newItems)
        {
            var mergedList = new ShoppingList
            {
                Id = existing.Id,
                UserId = existing.UserId,
                FamilyId = existing.FamilyId,
                StoreItems = new Dictionary<string, List<string>>(existing.StoreItems),
                CreatedAt = existing.CreatedAt,
                LastUpdated = DateTime.UtcNow
            };

            foreach (var (store, items) in newItems)
            {
                var normalizedStore = StoreNameNormalizer.NormalizeStoreName(store);
                if (!mergedList.StoreItems.ContainsKey(normalizedStore))
                {
                    mergedList.StoreItems[normalizedStore] = new List<string>();
                }

                mergedList.StoreItems[normalizedStore].AddRange(
                    items.Where(item => !mergedList.StoreItems[normalizedStore]
                        .Contains(item, StringComparer.OrdinalIgnoreCase))
                );
            }

            return mergedList;
        }

        public Task<ShoppingList> GetExistingShoppingList(string familyId)
        {
            throw new NotImplementedException();
        }

        public Task<Dictionary<string, List<string>>> AnalyzeReceiptsWithOpenAI(List<ReceiptDocument> receipts)
        {
            throw new NotImplementedException();
        }

        public async Task<Dictionary<string, List<string>>> AnalyzeReceiptWithOpenAI(string receiptId)
        {
            try
            {
                var receipt= await _receiptRepository.FetchRecipet(receiptId);
                var combinedText = string.Join("", receipt.FirstOrDefault().ReceiptText);
                return await _openAIService.AnalyzeReceiptWithOpenAIAsync(combinedText);

            }
            catch (Exception ex)
            {
                return new Dictionary<string, List<string>>();
            }
        }

        public Task<List<ReceiptDocument>> FetchUnprocessedReceipts(string familyId)
        {
            throw new NotImplementedException();
        }
    } 
     
} 