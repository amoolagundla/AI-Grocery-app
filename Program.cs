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

        // Add Service Bus client with validation
        var notificationQueueConnectionString = Environment.GetEnvironmentVariable("NotificaitonQueueConnectionString");
        var receiptAnalysisQueueName = Environment.GetEnvironmentVariable("ReceiptAnalysisQueueName") ?? "receipt-analysis-queue";

        if (string.IsNullOrEmpty(notificationQueueConnectionString))
        {
            throw new InvalidOperationException("NotificaitonQueueConnectionString configuration is missing");
        }
        var serviceBusConnectionString = Environment.GetEnvironmentVariable("NotificaitonQueueConnectionString");
        if (string.IsNullOrEmpty(serviceBusConnectionString))
        {
            throw new InvalidOperationException("NotificaitonQueueConnectionString configuration is missing");
        }
        var connectionStringBuilder = new ServiceBusConnectionStringBuilder(serviceBusConnectionString);
        var queueName = connectionStringBuilder.EntityPath;

        if (string.IsNullOrEmpty(queueName))
        {
            queueName = Environment.GetEnvironmentVariable("ReceiptAnalysisQueueName") ?? "receipt-analysis-queue";
        }

        services.AddSingleton(s => new ServiceBusClient(serviceBusConnectionString));
        services.AddSingleton(s =>
        {
            var client = s.GetRequiredService<ServiceBusClient>();
            return client.CreateSender(queueName);
        });
        services.AddHttpClient();
        // Register services
        services.AddScoped<IReceiptService, ReceiptService>();
        services.AddScoped<IBlobService, BlobService>();
        services.AddScoped<IOCRService, OCRService>();
        services.AddScoped<IReceiptProcessingService, ReceiptProcessingService>();
        services.AddScoped<IReceiptRepository, ReceiptRepository>();
        services.AddScoped<IShoppingListRepository, ShoppingListRepository>();
        services.AddScoped<IOpenAIService, OpenAIService>();
        services.AddScoped<INotificationService, NotificationService>();
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