using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OCR_AI_Grocery.Notifications;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddCors();
        services.AddSingleton<HttpClient>();
        services.AddSingleton<FirebaseMessagingService>();
        services.AddSingleton<UserDeviceTokenService>();
        var cosmosDbConnection = Environment.GetEnvironmentVariable("CosmosDBConnectionString");
        services.AddSingleton(s => new CosmosClient(cosmosDbConnection));
    }) 
    .Build();

host.Run();
 