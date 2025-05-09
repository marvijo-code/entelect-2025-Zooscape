using BasicBot.Models;
using BasicBot.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BasicBot;

public class Program
{
    public static IConfigurationRoot? Configuration;
    private static readonly ILogger _logger = new LoggerFactory().CreateLogger<Program>();

    private static async Task Main(string[] args)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory) // Ensures appsettings.json is found
            .AddJsonFile($"appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables();

        Configuration = builder.Build();

        // Read settings from configuration
        var runnerIpConfig =
            Environment.GetEnvironmentVariable("RUNNER_IPV4") ?? Configuration["RunnerIP"];
        var runnerPortConfig =
            Environment.GetEnvironmentVariable("RUNNER_PORT") ?? Configuration["RunnerPort"];
        var botNickname =
            Environment.GetEnvironmentVariable("BOT_NICKNAME") ?? Configuration["BotNickname"];
        var hubName = Environment.GetEnvironmentVariable("HUB_NAME") ?? Configuration["HubName"];
        var botToken =
            Environment.GetEnvironmentVariable("BOT_TOKEN")
            ?? Configuration["BotToken"]
            ?? Guid.NewGuid().ToString();

        // Validate essential configurations
        if (
            string.IsNullOrEmpty(runnerIpConfig)
            || string.IsNullOrEmpty(runnerPortConfig)
            || string.IsNullOrEmpty(botNickname)
            || string.IsNullOrEmpty(hubName)
            || string.IsNullOrEmpty(botToken)
        )
        {
            Console.WriteLine(
                "Error: RunnerIP, RunnerPort, BotNickname, HubName, or BotToken is not configured. Set them in appsettings.json or as environment variables RUNNER_IPV4, RUNNER_PORT, BOT_NICKNAME, HUB_NAME, BOT_TOKEN."
            );
            return;
        }

        string connectionUrl = $"{runnerIpConfig}:{runnerPortConfig}/{hubName}";

        Console.WriteLine($"Bot Nickname: {botNickname}");

        _logger.LogInformation($"Bot Nickname: {botNickname}");
        _logger.LogInformation($"Connecting to: {connectionUrl}");
        Console.WriteLine($"zzzConnecting to: {connectionUrl}");

        var connection = new HubConnectionBuilder()
            .WithUrl(connectionUrl)
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
                logging.AddConsole();
            })
            .WithAutomaticReconnect()
            .Build();

        var botService = new BotService();

        BotCommand botCommand = new BotCommand();

        connection.On<Guid>("Registered", (id) => botService.SetBotId(id));

        connection.On<GameState>(
            "GameState",
            (gamestate) =>
            {
                botCommand = botService.ProcessState(gamestate);
            }
        );

        connection.On<String>(
            "Disconnect",
            async (reason) =>
            {
                _logger.LogInformation($"Server sent disconnect with reason: {reason}");
                await connection.StopAsync();
            }
        );

        connection.Closed += async (error) =>
        {
            _logger.LogError(
                $"Connection closed. Error: {error?.Message}. Attempting to reconnect..."
            );
            await Task.Delay(new Random().Next(0, 5) * 1000); // Random delay before reconnect
            try
            {
                await connection.StartAsync();
                Console.WriteLine("Sent Register message");
                _logger.LogInformation("Reconnected. Attempting to register again...");
                await connection.InvokeAsync("Register", botToken, botNickname);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Reconnection failed: {ex.Message}");
            }
        };

        try
        {
            await connection.StartAsync();
            _logger.LogInformation("Connection started. Attempting to register...");
            await connection.InvokeAsync("Register", botToken, botNickname);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error starting connection or registering: {ex.Message}");
            return; // Exit if connection fails
        }

        while (
            connection.State == HubConnectionState.Connected
            || connection.State == HubConnectionState.Connecting
        )
        {
            if (
                botCommand == null
                || botCommand.Action < Enums.BotAction.Up
                || botCommand.Action > Enums.BotAction.Right
            )
            {
                continue;
            }
            try
            {
                await connection.SendAsync("BotCommand", botCommand);
                _logger.LogInformation($"Sent BotCommand: Action={botCommand.Action}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending BotCommand: {ex.Message}");
            }
            botCommand = null;
            await Task.Delay(100); // Loop delay
        }

        _logger.LogInformation("Application is shutting down.");
    }
}
