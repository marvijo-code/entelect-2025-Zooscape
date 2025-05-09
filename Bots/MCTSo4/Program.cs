using MCTSo4.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MCTSo4
{
    public class Program
    {
        private static IConfigurationRoot? Configuration;
        private static ILogger<Program>? _logger;
        private static MCTSo4Logic? _logic;

        public static async Task Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            var serviceProvider = serviceCollection.BuildServiceProvider();
            _logger = serviceProvider.GetService<ILogger<Program>>();

            if (_logger == null)
            {
                Console.WriteLine("Error: Logger could not be initialized.");
                return;
            }

            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();

            var runnerIpConfig =
                Environment.GetEnvironmentVariable("RUNNER_IPV4") ?? Configuration["RunnerIP"];
            var runnerPortConfig =
                Environment.GetEnvironmentVariable("RUNNER_PORT") ?? Configuration["RunnerPort"];
            var botNickname =
                Environment.GetEnvironmentVariable("BOT_NICKNAME") ?? Configuration["BotNickname"];
            Console.WriteLine($"Bot Nickname: {botNickname}");
            var hubName =
                Environment.GetEnvironmentVariable("HUB_NAME") ?? Configuration["HubName"];
            var botToken =
                Environment.GetEnvironmentVariable("BOT_TOKEN")
                ?? Configuration["BotToken"]
                ?? Guid.NewGuid().ToString();

            Console.WriteLine("RUNNER_IPV4: " + Environment.GetEnvironmentVariable("RUNNER_IPV4"));
            Console.WriteLine("RUNNER_PORT: " + Environment.GetEnvironmentVariable("RUNNER_PORT"));
            Console.WriteLine(
                "BOT_NICKNAME: " + Environment.GetEnvironmentVariable("BOT_NICKNAME")
            );
            Console.WriteLine("HUB_NAME: " + Environment.GetEnvironmentVariable("HUB_NAME"));
            Console.WriteLine("BOT_TOKEN: " + Environment.GetEnvironmentVariable("BOT_TOKEN"));

            if (
                string.IsNullOrEmpty(runnerIpConfig)
                || string.IsNullOrEmpty(runnerPortConfig)
                || string.IsNullOrEmpty(botNickname)
                || string.IsNullOrEmpty(hubName)
                || string.IsNullOrEmpty(botToken)
            )
            {
                _logger.LogError(
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

            _logger.LogInformation($"Bot Nickname: {botNickname}");
            _logger.LogInformation($"Attempting to connect to: {connectionUrl}");

            Console.WriteLine($"runnerIpConfig: {runnerIpConfig}");
            Console.WriteLine($"runnerPortConfig: {runnerPortConfig}");
            Console.WriteLine($"Attempting to connect to: {connectionUrl}");

            var connection = new HubConnectionBuilder()
                .WithUrl(connectionUrl)
                .ConfigureLogging(logging =>
                {
                    // Set a minimum log level. Actual output depends on registered providers.
                    // Add Microsoft.Extensions.Logging.Console/Debug NuGet packages for console/debug output.
                    logging.SetMinimumLevel(LogLevel.Information);
                    logging.AddConsole();
                })
                .WithAutomaticReconnect()
                .Build();

            _logic = new MCTSo4Logic(connection);

            connection.Closed += async (error) =>
            {
                _logger.LogError(
                    $"Connection closed. Error: {error?.Message}. Attempting to reconnect..."
                );
                await Task.Delay(new Random().Next(0, 5) * 1000);
                try
                {
                    await connection.StartAsync();
                    _logger.LogInformation("Reconnected. Attempting to register again...");
                    if (_logic != null)
                    {
                        await _logic.StartAsync(botToken, botNickname);
                    }
                    else
                    {
                        _logger.LogError("Logic service not initialized, cannot re-register.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Reconnection failed: {ex.Message}");
                }
            };

            try
            {
                await connection.StartAsync();
                _logger.LogInformation("Connection started successfully.");

                if (_logic != null)
                {
                    await _logic.StartAsync(botToken, botNickname);
                }
                else
                {
                    _logger.LogError("Logic service not initialized at startup.");
                    return;
                }

                await Task.Delay(Timeout.Infinite);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to connect or error during bot operation: {ex.Message}");
            }
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(builder =>
            {
                // Add Microsoft.Extensions.Logging.Console and .Debug NuGet packages to the .csproj
                // for these lines to work and provide console/debug logging.
                // builder.AddConsole();
                // builder.AddDebug();
            });
        }
    }
}
