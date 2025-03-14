using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using OCR_AI_Grocery.Models;
using OCR_AI_Grocery.Models.Receipt;
using OCR_AI_Grocey.Services.Helpers;
using OCR_AI_Grocey.Services.Implementations;
using OCR_AI_Grocey.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Moq.Language;
using Moq.Protected;
using StorageDiagnostics = OCR_AI_Grocery.Models.StorageDiagnostics;

namespace OCR_AI_Grocery_Tests
{
    public class ReceiptProcessingServiceTests
    {
        private readonly Mock<ILogger<ReceiptProcessingService>> _loggerMock;
        private readonly Mock<IReceiptService> _receiptServiceMock;
        private readonly Mock<IBlobService> _blobServiceMock;
        private readonly Mock<IOCRService> _ocrServiceMock;
        private readonly AnalysisSender _analysisSender;
        private readonly Mock<IAnalysisQueue> _analysisQueueMock;

        private readonly ReceiptProcessingService _service;

        public ReceiptProcessingServiceTests()
        {
            _loggerMock = new Mock<ILogger<ReceiptProcessingService>>();
            _receiptServiceMock = new Mock<IReceiptService>();
            _blobServiceMock = new Mock<IBlobService>();
            _ocrServiceMock = new Mock<IOCRService>();

            // Create a mock of ServiceBusSender to pass to AnalysisSender
            var serviceBusSenderMock = new Mock<ServiceBusSender>();
            _analysisSender = new AnalysisSender(serviceBusSenderMock.Object);

            _analysisQueueMock = new Mock<IAnalysisQueue>();

            _service = new ReceiptProcessingService(
                _loggerMock.Object,
                _receiptServiceMock.Object,
                _blobServiceMock.Object,
                _ocrServiceMock.Object,
                _analysisSender,
                _analysisQueueMock.Object
            );
        }

        [Fact]
        public async Task ProcessSingleEvent_ValidData_CompletesSuccessfully()
        {
            // Arrange
            var eventGridEvent = CreateSampleEventGridEvent();
            string eventJson = "[" + JsonConvert.SerializeObject(eventGridEvent) + "]";

            var blobContent = new MemoryStream(new byte[] { 1, 2, 3, 4 });
            var metadata = new Dictionary<string, string>
            {
                { "email", "test@example.com" },
                { "familyId", "family123" }
            };

            _blobServiceMock
                .Setup(x => x.DownloadBlobWithMetadataAsync(It.IsAny<string>()))
                .ReturnsAsync((blobContent, metadata as IDictionary<string, string>));

            _ocrServiceMock
                .Setup(x => x.PerformOCR(It.IsAny<Stream>()))
                .ReturnsAsync("Sample OCR Text");

            _receiptServiceMock
                .Setup(x => x.SaveReceiptAsync(It.IsAny<ReceiptDocument>()))
                .Returns(Task.CompletedTask);

            _analysisQueueMock
                .Setup(x => x.SendToAnalysisQueue(It.IsAny<IDictionary<string, string>>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.ProcessSingleEvent(eventJson);

            // Assert
            _blobServiceMock.Verify(x => x.DownloadBlobWithMetadataAsync(It.Is<string>(s => s == eventGridEvent.Data.Url)), Times.Once);
            _ocrServiceMock.Verify(x => x.PerformOCR(It.IsAny<Stream>()), Times.Once);
            _receiptServiceMock.Verify(x => x.SaveReceiptAsync(It.Is<ReceiptDocument>(r =>
                r.UserId == "test@example.com" &&
                r.FamilyId == "family123" &&
                r.ReceiptText == "Sample OCR Text" &&
                r.BlobUrl == eventGridEvent.Data.Url
            )), Times.Once);
            _analysisQueueMock.Verify(x => x.SendToAnalysisQueue(It.IsAny<IDictionary<string, string>>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ProcessSingleEvent_NullEventData_LogsWarningAndReturns()
        {
            // Arrange
            string eventJson = "[]"; // Empty array will deserialize to empty list

            // Act
            await _service.ProcessSingleEvent(eventJson);

            // Assert
            _blobServiceMock.Verify(x => x.DownloadBlobWithMetadataAsync(It.IsAny<string>()), Times.Never);
            _ocrServiceMock.Verify(x => x.PerformOCR(It.IsAny<Stream>()), Times.Never);
            _receiptServiceMock.Verify(x => x.SaveReceiptAsync(It.IsAny<ReceiptDocument>()), Times.Never);
            _analysisQueueMock.Verify(x => x.SendToAnalysisQueue(It.IsAny<IDictionary<string, string>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ProcessSingleEvent_EmptyBlobUrl_LogsWarningAndReturns()
        {
            // Arrange
            var eventGridEvent = CreateSampleEventGridEvent();
            eventGridEvent.Data.Url = ""; // Empty URL
            string eventJson = "[" + JsonConvert.SerializeObject(eventGridEvent) + "]";

            // Act
            await _service.ProcessSingleEvent(eventJson);

            // Assert
            _blobServiceMock.Verify(x => x.DownloadBlobWithMetadataAsync(It.IsAny<string>()), Times.Never);
            _ocrServiceMock.Verify(x => x.PerformOCR(It.IsAny<Stream>()), Times.Never);
            _receiptServiceMock.Verify(x => x.SaveReceiptAsync(It.IsAny<ReceiptDocument>()), Times.Never);
            _analysisQueueMock.Verify(x => x.SendToAnalysisQueue(It.IsAny<IDictionary<string, string>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ProcessSingleEvent_NullBlobContent_ReturnsEarly()
        {
            // Arrange
            var eventGridEvent = CreateSampleEventGridEvent();
            string eventJson = "[" + JsonConvert.SerializeObject(eventGridEvent) + "]";

            _blobServiceMock
                .Setup(x => x.DownloadBlobWithMetadataAsync(It.IsAny<string>()))
                .ReturnsAsync((null as Stream, new Dictionary<string, string>() as IDictionary<string, string>));

            // Act
            await _service.ProcessSingleEvent(eventJson);

            // Assert
            _ocrServiceMock.Verify(x => x.PerformOCR(It.IsAny<Stream>()), Times.Never);
            _receiptServiceMock.Verify(x => x.SaveReceiptAsync(It.IsAny<ReceiptDocument>()), Times.Never);
            _analysisQueueMock.Verify(x => x.SendToAnalysisQueue(It.IsAny<IDictionary<string, string>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ProcessSingleEvent_ExceptionThrown_LogsErrorAndRethrows()
        {
            // Arrange
            var eventGridEvent = CreateSampleEventGridEvent();
            string eventJson = "[" + JsonConvert.SerializeObject(eventGridEvent) + "]";

            _blobServiceMock
                .Setup(x => x.DownloadBlobWithMetadataAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Test exception"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _service.ProcessSingleEvent(eventJson));
        }

        [Fact]
        public async Task ProcessSingleEvent_MissingMetadata_UsesDefaultValues()
        {
            // Arrange
            var eventGridEvent = CreateSampleEventGridEvent();
            string eventJson = "[" + JsonConvert.SerializeObject(eventGridEvent) + "]";

            var blobContent = new MemoryStream(new byte[] { 1, 2, 3, 4 });
            var emptyMetadata = new Dictionary<string, string>(); // No metadata

            _blobServiceMock
                .Setup(x => x.DownloadBlobWithMetadataAsync(It.IsAny<string>()))
                .ReturnsAsync((blobContent, emptyMetadata as IDictionary<string, string>));

            _ocrServiceMock
                .Setup(x => x.PerformOCR(It.IsAny<Stream>()))
                .ReturnsAsync("Sample OCR Text");

            _receiptServiceMock
                .Setup(x => x.SaveReceiptAsync(It.IsAny<ReceiptDocument>()))
                .Returns(Task.CompletedTask);

            _analysisQueueMock
                .Setup(x => x.SendToAnalysisQueue(It.IsAny<IDictionary<string, string>>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.ProcessSingleEvent(eventJson);

            // Assert
            _receiptServiceMock.Verify(x => x.SaveReceiptAsync(It.Is<ReceiptDocument>(r =>
                r.UserId == "Unknown" &&
                r.FamilyId == "Unknown"
            )), Times.Once);
        }

        [Fact]
        public async Task ProcessReceiptEvents_MultipleEvents_ProcessesAllEvents()
        {
            // Arrange
            var event1 = CreateSampleEventGridEvent();
            var event2 = CreateSampleEventGridEvent();
            event2.Id = "different-id";

            string[] events = new[] {
                "[" + JsonConvert.SerializeObject(event1) + "]",
                "[" + JsonConvert.SerializeObject(event2) + "]"
            };

            var blobContent = new MemoryStream(new byte[] { 1, 2, 3, 4 });
            var metadata = new Dictionary<string, string>
            {
                { "email", "test@example.com" },
                { "familyId", "family123" }
            };

            _blobServiceMock
                .Setup(x => x.DownloadBlobWithMetadataAsync(It.IsAny<string>()))
                .ReturnsAsync((blobContent, metadata as IDictionary<string, string>));

            _ocrServiceMock
                .Setup(x => x.PerformOCR(It.IsAny<Stream>()))
                .ReturnsAsync("Sample OCR Text");

            _receiptServiceMock
                .Setup(x => x.SaveReceiptAsync(It.IsAny<ReceiptDocument>()))
                .Returns(Task.CompletedTask);

            _analysisQueueMock
                .Setup(x => x.SendToAnalysisQueue(It.IsAny<IDictionary<string, string>>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.ProcessReceiptEvents(events);

            // Assert
            _blobServiceMock.Verify(x => x.DownloadBlobWithMetadataAsync(It.IsAny<string>()), Times.Exactly(2));
            _ocrServiceMock.Verify(x => x.PerformOCR(It.IsAny<Stream>()), Times.Exactly(2));
            _receiptServiceMock.Verify(x => x.SaveReceiptAsync(It.IsAny<ReceiptDocument>()), Times.Exactly(2));
            _analysisQueueMock.Verify(x => x.SendToAnalysisQueue(It.IsAny<IDictionary<string, string>>(), It.IsAny<string>()), Times.Exactly(2));
        }

        private EventGridEvent CreateSampleEventGridEvent()
        {
            return new EventGridEvent
            {
                Topic = "/subscriptions/fb0cd0f2-2e8c-4ae7-ba8d-99fd413ddc77/resourceGroups/AI-Grocery/providers/Microsoft.Storage/storageAccounts/reciepts",
                Subject = "/blobServices/default/containers/receipts/blobs/receipt_dba59c4e-a8e7-4fb2-86c5-2fe3f191c20c.jpg",
                EventType = "Microsoft.Storage.BlobCreated",
                Id = "eb31b044-201e-0013-429f-8fc17606c9ec",
                Data = new EventGridData
                {
                    Api = "PutBlob",
                    ClientRequestId = null,
                    RequestId = "eb31b044-201e-0013-429f-8fc176000000",
                    ETag = "0x8DD5DB68721689A",
                    ContentType = "image/jpeg",
                    ContentLength = 5134925,
                    BlobType = "BlockBlob",
                    AccessTier = "Default",
                    Url = "https://reciepts.blob.core.windows.net/receipts/receipt_dba59c4e-a8e7-4fb2-86c5-2fe3f191c20c.jpg",
                    Sequencer = "00000000000000000000000000013DC50000000000f2b895",
                    StorageDiagnostics = new StorageDiagnostics
                    {
                        BatchId = "1521a532-2006-00f7-009f-8fcfe8000000"
                    }
                },
                DataVersion = "",
                MetadataVersion = "1",
                EventTime = DateTime.Parse("2025-03-07T20:27:48.9033073Z")
            };
        }
    }
}