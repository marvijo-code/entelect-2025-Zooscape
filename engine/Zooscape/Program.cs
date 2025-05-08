using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using S3Logger;
using S3Logger.Events;
using S3Logger.Utilities;
using Serilog;
using Serilog.Extensions.Logging;
using Serilog.Sinks.File.GZip;
using Zooscape.Application;
using Zooscape.Application.Config;
using Zooscape.Application.Events;
using Zooscape.Application.Services;
using Zooscape.Infrastructure.CloudIntegration.Enums;
using Zooscape.Infrastructure.CloudIntegration.Events;
using Zooscape.Infrastructure.CloudIntegration.Models;
using Zooscape.Infrastructure.CloudIntegration.Services;
using Zooscape.Infrastructure.SignalRHub.Config;
using Zooscape.Infrastructure.SignalRHub.Events;
using Zooscape.Infrastructure.SignalRHub.Hubs;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

Log.Information("Initialising cloud integration");

// Initialize CloudIntegrationService manually so we can announce failures early
var cloudLog = new SerilogLoggerFactory(Log.Logger).CreateLogger<CloudIntegrationService>();
CloudSettings cloudSettings = new();
CloudIntegrationService cloudIntegrationService = new(cloudSettings, cloudLog);

Log.Information("Announcing initialisation to cloud");
await cloudIntegrationService.Announce(CloudCallbackType.Initializing);
try
{
    Log.Information("Initialising host");
    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog(
            (context, _, serilogConfig) =>
                serilogConfig.ReadFrom.Configuration(context.Configuration).Enrich.FromLogContext()
        )
        .ConfigureServices(
            (context, services) =>
            {
                // Cloud Integration
                services.AddSingleton<ICloudIntegrationService>(cloudIntegrationService);

                // SignalR
                services.Configure<SignalRConfigOptions>(
                    context.Configuration.GetSection("SignalR")
                );
                services.AddSignalR(options =>
                {
                    options.EnableDetailedErrors = true;
                    options.MaximumReceiveMessageSize = 40000000;
                    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
                });

                // CORS Configuration
                services.AddCors(options =>
                {
                    options.AddPolicy(
                        name: "AllowZooscapeVisualizer", // You can name this policy
                        policy =>
                        {
                            policy
                                .WithOrigins("http://localhost:5173") // Your React app's origin
                                .AllowAnyHeader()
                                .AllowAnyMethod()
                                .AllowCredentials(); // Important for SignalR
                        }
                    );
                });

                services.Configure<GameSettings>(context.Configuration.GetSection("GameSettings"));

                services.Configure<S3Configuration>(
                    context.Configuration.GetSection("S3Configuration")
                );

                services.AddKeyedSingleton<IEventDispatcher, SignalREventDispatcher>("signalr");
                services.AddKeyedSingleton<IEventDispatcher, CloudEventDispatcher>("cloud");
                services.AddKeyedSingleton<IEventDispatcher, LogStateEventDispatcher>("logState");

                // per-match log folder
                var defaultLogRoot = Path.Combine(AppContext.BaseDirectory, "logs");
                var logRoot = Environment.GetEnvironmentVariable("LOG_DIR") ?? defaultLogRoot;
                var matchId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var matchDir = Path.Combine(logRoot, matchId);
                Directory.CreateDirectory(matchDir);
                S3.LogDirectory = matchDir;

                services.AddSingleton<IStreamingFileLogger>(
                    new StreamingFileLogger(S3.LogDirectory, "gameLogs.log")
                );

                services.AddSingleton<IZookeeperService, ZookeeperService>();

                services.AddTransient<BotHub>();

                services.AddSingleton<IGameStateService, GameStateService>();

                services.AddHostedService<WorkerService>();
            }
        )
        .ConfigureWebHostDefaults(webBuilder =>
        {
            var port = 5000;
            Log.Information("Configuring SignalR to run on port {port}", port);
            webBuilder.UseUrls($"http://*:{port}");
            webBuilder.Configure(app =>
            {
                // Apply the CORS policy - must be before UseRouting and UseEndpoints
                app.UseCors("AllowZooscapeVisualizer");

                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapHub<BotHub>("/bothub");
                });
            });
        })
        .Build();
    Log.Information("Running host");
    host.Run();
    Log.Information("Host shutting down");
}
catch (Exception ex)
{
    Log.Fatal($"Error starting host: {ex}");
    await cloudIntegrationService.Announce(CloudCallbackType.Failed, ex);
}
finally
{
    Log.CloseAndFlush();
    await S3.UploadLogs();
    await cloudIntegrationService.Announce(CloudCallbackType.LoggingComplete);
}
