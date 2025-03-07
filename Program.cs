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

        // Add Cosmos DB client
        var cosmosDbConnection = Environment.GetEnvironmentVariable("CosmosDBConnectionString");
        services.AddSingleton(s => new CosmosClient(cosmosDbConnection));

        // Add Service Bus client
        var notificationQueueConnectionString = Environment.GetEnvironmentVariable("NotificaitonQueueConnectionString");
        services.AddSingleton(s => new ServiceBusClient(notificationQueueConnectionString));

        // Register activity functions
        services.AddSingleton<AnalyzeUserReceiptsActivityFunction>();
        services.AddScoped<IReceiptService, ReceiptService>(); 
        services.AddScoped<IBlobService, BlobService>();
        services.AddScoped<IOCRService, OCRService>();

        // Add logging
        services.AddLogging();
    })
    .Build();

host.Run();