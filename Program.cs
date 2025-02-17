using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
                builder.WithOrigins("http://localhost:4200")  // 👈 Specify your allowed localhost
                       .AllowAnyMethod()  // Allows all HTTP methods (GET, POST, etc.)
                       .AllowAnyHeader()  // Allows all headers
            );
        });

    })
    .Build();

host.Run();
 
