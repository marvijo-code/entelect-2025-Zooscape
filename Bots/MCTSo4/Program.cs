using MCTSo4.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging; // Added for LogLevel and SetMinimumLevel
// using Microsoft.Extensions.Logging; // Replaced by Serilog - This comment is now potentially confusing, but the using is needed for SignalR configuration
using Serilog; // Added for Serilog

namespace MCTSo4
{
    public class Program
    {
        private static IConfigurationRoot? Configuration;

        // private static ILogger<Program>? _logger; // Replaced by Serilog static logger
        private static MCTSo4Logic? _logic;

        public static async Task Main(string[] args)
        {
            // Configure Serilog logger from appsettings.json
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(Configuration)
                .Enrich.FromLogContext()
                // .WriteTo.Console() // Removed: Console sink is configured in appsettings.json
                .WriteTo.File(
                    "logs/mctso4-.txt",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7
                )
                .CreateLogger();

            // Replace previous logger initialization
            // var serviceCollection = new ServiceCollection();
            // ConfigureServices(serviceCollection); // Service configuration can be simplified or integrated with HostBuilder if needed later
            // var serviceProvider = serviceCollection.BuildServiceProvider();
            // _logger = serviceProvider.GetService<ILogger<Program>>();

            // if (_logger == null)
            // {
            //     Console.WriteLine("Error: Logger could not be initialized.");
            //     return;
            // }

            // Use Serilog's static Log class for logging
            // Example: Log.Information("This is an informational message.");

            try
            {
                Log.Information("MCTSo4 Bot starting up...");

                var runnerIpConfig =
                    Environment.GetEnvironmentVariable("RUNNER_IPV4") ?? Configuration["RunnerIP"];
                var runnerPortConfig =
                    Environment.GetEnvironmentVariable("RUNNER_PORT")
                    ?? Configuration["RunnerPort"];
                var botNickname =
                    Environment.GetEnvironmentVariable("BOT_NICKNAME")
                    ?? Configuration["BotNickname"];
                Log.Information($"Bot Nickname from config: {botNickname}");
                var hubName =
                    Environment.GetEnvironmentVariable("HUB_NAME") ?? Configuration["HubName"];
                var botToken =
                    Environment.GetEnvironmentVariable("Token")
                    ?? Configuration["BotToken"]
                    ?? Guid.NewGuid().ToString();

                Log.Debug(
                    "RUNNER_IPV4: {RunnerIPV4}",
                    Environment.GetEnvironmentVariable("RUNNER_IPV4")
                );
                Log.Debug(
                    "RUNNER_PORT: {RunnerPort}",
                    Environment.GetEnvironmentVariable("RUNNER_PORT")
                );
                Log.Debug(
                    "BOT_NICKNAME: {BotNicknameEnv}",
                    Environment.GetEnvironmentVariable("BOT_NICKNAME")
                );
                Log.Debug("HUB_NAME: {HubNameEnv}", Environment.GetEnvironmentVariable("HUB_NAME"));
                Log.Debug("BOT_TOKEN: {BotTokenEnv}", Environment.GetEnvironmentVariable("Token"));

                if (
                    string.IsNullOrEmpty(runnerIpConfig)
                    || string.IsNullOrEmpty(runnerPortConfig)
                    || string.IsNullOrEmpty(botNickname)
                    || string.IsNullOrEmpty(hubName)
                    || string.IsNullOrEmpty(botToken)
                )
                {
                    Log.Error(
                        "Error: RunnerIP, RunnerPort, BotNickname, HubName, or BotToken is not configured. "
                            + "Set them in appsettings.json or as environment variables "
                            + "RUNNER_IPV4, RUNNER_PORT, BOT_NICKNAME, HUB_NAME, BOT_TOKEN."
                    );
                    return;
                }

                if (!runnerIpConfig.StartsWith("http://") && !runnerIpConfig.StartsWith("https://"))
                {
                    runnerIpConfig = "http://" + runnerIpConfig;
                }

                string connectionUrl = $"{runnerIpConfig}:{runnerPortConfig}/{hubName}";

                Log.Information("Bot Nickname to be used: {BotNickname}", botNickname);
                Log.Information("Attempting to connect to: {ConnectionUrl}", connectionUrl);

                // Remove old Console.WriteLine statements
                // Console.WriteLine($"runnerIpConfig: {runnerIpConfig}");
                // Console.WriteLine($"runnerPortConfig: {runnerPortConfig}");
                // Console.WriteLine($"Attempting to connect to: {connectionUrl}");

                var connection = new HubConnectionBuilder()
                    .WithUrl(connectionUrl)
                    .ConfigureLogging(logging => // SignalR logging, can keep or remove if Serilog captures it
                    {
                        logging.AddSerilog(); // Integrate Serilog with SignalR's logging
                        logging.SetMinimumLevel(LogLevel.Information); // Or another level as needed
                    })
                    .WithAutomaticReconnect()
                    .Build();

                // It's good practice to pass ILogger instances via DI,
                // but for now, we can create a logger for MCTSo4Logic if needed, or it can use Log.ForContext<MCTSo4Logic>()
                // For simplicity in this step, MCTSo4Logic can use the static Serilog.Log instance.
                _logic = new MCTSo4Logic(connection); // Assuming MCTSo4Logic will use Serilog.Log internally

                connection.Closed += async (error) =>
                {
                    Log.Error(
                        error,
                        "Connection closed. Error: {ErrorMessage}. Attempting to reconnect...",
                        error?.Message
                    );
                    await Task.Delay(new Random().Next(0, 5) * 1000);
                    try
                    {
                        await connection.StartAsync();
                        Log.Information("Reconnected. Attempting to register again...");
                        if (_logic != null)
                        {
                            await _logic.StartAsync(botToken, botNickname);
                        }
                        else
                        {
                            Log.Error("Logic service not initialized, cannot re-register.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Reconnection failed: {ErrorMessage}", ex.Message);
                    }
                };

                // try // This try is now the outer try block
                // {
                await connection.StartAsync();
                Log.Information("Connection started successfully.");

                if (_logic != null)
                {
                    await _logic.StartAsync(botToken, botNickname);
                }
                else
                {
                    Log.Error("Logic service not initialized at startup.");
                    return;
                }

                await Task.Delay(Timeout.Infinite);
                // }
                // catch (Exception ex)
                // {
                //     Log.Error(ex, "Failed to connect or error during bot operation: {ErrorMessage}", ex.Message);
                // }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "MCTSo4 Bot terminated unexpectedly.");
            }
            finally
            {
                Log.Information("MCTSo4 Bot shutting down...");
                Log.CloseAndFlush();
            }
        }

        // ConfigureServices is not strictly necessary for basic Serilog setup if not using HostBuilder
        // private static void ConfigureServices(IServiceCollection services)
        // {
        //     services.AddLogging(builder =>
        //     {
        //         // builder.AddSerilog(); // This would be used with HostBuilder approach
        //     });
        // }
    }
}
