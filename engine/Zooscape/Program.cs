using System;
using System.Globalization;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Zooscape.Application.Config;
using Zooscape.Application.Events;
using Zooscape.Application.Services;
using Zooscape.Domain.Interfaces;
using Zooscape.Domain.Utilities;
using Zooscape.Infrastructure.CloudIntegration.Enums;
using Zooscape.Infrastructure.CloudIntegration.Events;
using Zooscape.Infrastructure.CloudIntegration.Models;
using Zooscape.Infrastructure.CloudIntegration.Services;
using Zooscape.Infrastructure.S3Logger;
using Zooscape.Infrastructure.S3Logger.Events;
using Zooscape.Infrastructure.S3Logger.Utilities;
using Zooscape.Infrastructure.SignalRHub.Config;
using Zooscape.Infrastructure.SignalRHub.Events;
using Zooscape.Infrastructure.SignalRHub.Hubs;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

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
                var seed = context.Configuration.GetSection("GameSettings").GetValue<int>("Seed");
                if (seed <= 0)
                    seed = new Random().Next();

                services.AddSingleton(new GlobalSeededRandomizer(seed));

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

                services.Configure<GameSettings>(context.Configuration.GetSection("GameSettings"));

                services.Configure<GameLogsConfiguration>(
                    context.Configuration.GetSection("GameLogsConfiguration")
                );

                services.AddKeyedSingleton<IEventDispatcher, SignalREventDispatcher>("signalr");
                services.AddKeyedSingleton<IEventDispatcher, CloudEventDispatcher>("cloud");
                services.AddKeyedSingleton<IEventDispatcher, LogStateEventDispatcher>("logState");
                services.AddKeyedSingleton<IEventDispatcher, LogDiffStateEventDispatcher>(
                    "logDiffState"
                );
                services.AddSingleton<IPowerUpService, PowerUpService>();
                services.AddSingleton<IObstacleService, ObstacleService>();

                S3.LogDirectory =
                    Environment.GetEnvironmentVariable("LOG_DIR")
                    ?? Path.Combine(AppContext.BaseDirectory, "logs");

                services.AddSingleton<IStreamingFileLogger>(
                    new StreamingFileLogger(
                        context
                            .Configuration.GetSection("GameLogsConfiguration")
                            .GetValue<bool>("FullLogsEnabled"),
                        S3.LogDirectory,
                        "gameLogs.log"
                    )
                );

                services.AddSingleton<IStreamingFileDiffLogger>(
                    new StreamingFileDiffLogger(
                        context
                            .Configuration.GetSection("GameLogsConfiguration")
                            .GetValue<bool>("DiffLogsEnabled"),
                        S3.LogDirectory,
                        "gameDiffLogs.log"
                    )
                );

                services.AddSingleton<IZookeeperService, ZookeeperService>();

                services.AddTransient<BotHub>();
                services.AddTransient<IAnimalFactory, AnimalFactory>();

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
