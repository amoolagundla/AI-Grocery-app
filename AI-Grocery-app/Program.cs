using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OCR_AI_Grocery;
using OCR_AI_Grocery.Notifications;
using OCR_AI_Grocery.services;
using Azure.Messaging.ServiceBus;
using OCR_AI_Grocey.Services.Implementations;
using OCR_AI_Grocey.Services.Interfaces;
using Microsoft.Azure.ServiceBus;
using OCR_AI_Grocey.Services.Repos;
using OCR_AI_Grocery.Services.Repositories;
using OCR_AI_Grocey.Services.Helpers;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        // Add base services
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddCors();

        // Add HTTP client
        services.AddHttpClient();
        services.AddSingleton<HttpClient>();

        // Add notification services
        services.AddSingleton<FirebaseMessagingService>();
        services.AddSingleton<UserDeviceTokenService>();

        // Add helper services
        services.AddSingleton<CleanJsonResponseHelper>();

        // Add Cosmos DB client with validation
        var cosmosDbConnection = Environment.GetEnvironmentVariable("CosmosDBConnectionString");
        if (string.IsNullOrEmpty(cosmosDbConnection))
        {
            throw new InvalidOperationException("CosmosDBConnectionString configuration is missing");
        }
        services.AddSingleton(s =>
        {
            var clientOptions = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            };
            return new CosmosClient(cosmosDbConnection, clientOptions);
        });

        var notificationQueueConnectionString = Environment.GetEnvironmentVariable("NotificaitonQueueConnectionString");
        if (string.IsNullOrEmpty(notificationQueueConnectionString))
        {
            throw new InvalidOperationException("NotificaitonQueueConnectionString configuration is missing");
        }

        var receiptQueueConnectionString = Environment.GetEnvironmentVariable("QueueConnectionString");
        if (string.IsNullOrEmpty(receiptQueueConnectionString))
        {
            throw new InvalidOperationException("QueueConnectionString configuration is missing");
        }

        // Register clients - we need separate clients since they use different connection strings
        services.AddSingleton(new ServiceBusClient(receiptQueueConnectionString));
        services.AddSingleton<NotificationClient>(new NotificationClient(notificationQueueConnectionString));

        // Register senders using wrapper classes to avoid DI conflicts
        services.AddSingleton<AnalysisSender>(new AnalysisSender(
            new ServiceBusClient(receiptQueueConnectionString).CreateSender("receipt-analysis-queue")
        ));

        services.AddSingleton<NotificationSender>(new NotificationSender(
            new ServiceBusClient(notificationQueueConnectionString).CreateSender("user-notifications-queue")
        ));

        // Add service registrations that depend on these senders
        services.AddScoped<IReceiptProcessingService, ReceiptProcessingService>();
        services.AddScoped<INotificationService, NotificationService>();

        services.AddHttpClient();
        // Register services
        services.AddScoped<IReceiptService, ReceiptService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IBlobService, BlobService>();
        services.AddScoped<IOCRService, OCRService>();
        services.AddScoped<IReceiptProcessingService, ReceiptProcessingService>();
        services.AddScoped<IReceiptRepository, ReceiptRepository>();
        services.AddScoped<IShoppingListRepository, ShoppingListRepository>();
        services.AddScoped<IOpenAIService, OpenAIService>();
        services.AddScoped<INotificationService, NotificationService>(); 
        services.AddSingleton<IFamilyRepository, FamilyRepository>();
        services.AddSingleton<IAnalysisQueue, AnalysisQueue>();
        services.AddScoped<IAnalyzeUserReceiptsService, AnalyzeUserReceiptsService>();   
        services.AddSingleton<CleanJsonResponseHelper>();
        // Register activity functions
        services.AddSingleton<AnalyzeUserReceiptsActivityFunction>(); 

        // Add logging with more detailed configuration
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
    })
    .Build();

try
{
    host.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"Critical error starting the application: {ex}");
    throw;
}

