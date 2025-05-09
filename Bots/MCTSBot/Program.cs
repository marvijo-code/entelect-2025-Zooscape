using System;
using System.Threading.Tasks;
using MCTSBot.Models; // For EngineGameState, EngineBotCommand
using MCTSBot.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MCTSBot;

public class Program
{
    public static IConfigurationRoot? Configuration;
    private static EngineBotCommand? _botCommandToSend; // Command to be sent
    private static readonly object _commandLock = new object(); // For thread-safe access to _botCommandToSend
    private static readonly ILogger _logger = new LoggerFactory().CreateLogger<Program>();

    private static async Task Main(string[] args)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory) // Ensures appsettings.json is found in deployed/published scenarios
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables();

        Configuration = builder.Build();

        // Read settings from configuration
        var runnerIpConfig =
            Environment.GetEnvironmentVariable("RUNNER_IPV4") ?? Configuration["RunnerIP"];
        var runnerPortConfig =
            Environment.GetEnvironmentVariable("RUNNER_PORT") ?? Configuration["RunnerPort"];
        var botNickname =
            Environment.GetEnvironmentVariable("BOT_NICKNAME") ?? Configuration["BotNickname"];
        Console.WriteLine($"Bot Nickname: {botNickname}");
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
            _logger.LogError(
                "Error: RunnerIP, RunnerPort, BotNickname, HubName, or BotToken is not configured. Set them in appsettings.json or as environment variables RUNNER_IPV4, RUNNER_PORT, BOT_NICKNAME, HUB_NAME, BOT_TOKEN."
            );
            return;
        }

        // MCTS Specific settings from root or defaults
        var mctsIterations = Configuration.GetValue<int>("MCTSIterations", 1000);
        var mctsExploration = Configuration.GetValue<double>("MCTSExplorationParam", 1.414);

        _logger.LogInformation($"BotNickname: {botNickname}");
        _logger.LogInformation($"RunnerIP for Connection: {runnerIpConfig}");
        _logger.LogInformation($"RunnerPort for Connection: {runnerPortConfig}");
        _logger.LogInformation($"HubName for Connection: {hubName}");

        // Construct the final URL for the SignalR connection
        string connectionUrl = $"{runnerIpConfig}:{runnerPortConfig}/{hubName}";
        _logger.LogInformation($"Attempting to connect to: {connectionUrl}");
        Console.WriteLine($"Attempting to connect to: {connectionUrl}");

        var connection = new HubConnectionBuilder()
            .WithUrl(connectionUrl) // Expects full URL like http://localhost:5000/runnerhub
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information); // Adjust log level as needed
                logging.AddConsole();
            })
            .WithAutomaticReconnect()
            .Build();

        var instructionService = new InstructionService();
        var botLogic = new MCTSBotLogic(instructionService, mctsIterations, mctsExploration);

        _botCommandToSend = null; // Initialize as null

        connection.On<Guid>(
            "Registered",
            (id) =>
            {
                _logger.LogInformation($"Successfully registered with ID: {id}");
                botLogic.SetBotId(id);
            }
        );

        connection.On<EngineGameState>(
            "GameState",
            (engineGameState) =>
            {
                _logger.LogInformation($"Received GameState for Tick: {engineGameState?.Tick}");
                EngineBotCommand newCommand = botLogic.ProcessState(engineGameState!);
                lock (_commandLock)
                {
                    _botCommandToSend = newCommand;
                }
            }
        );

        connection.On<string>(
            "Disconnect",
            async (reason) =>
            {
                _logger.LogInformation($"Server sent disconnect with reason: {reason}");
                await connection.StopAsync();
            }
        );

        connection.Closed += (error) =>
        {
            _logger.LogInformation($"Connection closed. Error: {error?.Message}");
            return Task.CompletedTask;
        };

        try
        {
            await connection.StartAsync();
            _logger.LogInformation("Connection started. Attempting to register...");

            await connection.InvokeAsync("Register", botToken, botNickname);
            _logger.LogInformation("Register message sent.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error starting connection or registering: {ex.Message}");
            return; // Exit if connection fails
        }

        // Main loop to send commands
        while (
            connection.State == HubConnectionState.Connected
            || connection.State == HubConnectionState.Reconnecting
        )
        {
            EngineBotCommand? commandToSend = null;
            lock (_commandLock)
            {
                if (_botCommandToSend != null)
                {
                    commandToSend = _botCommandToSend;
                    _botCommandToSend = null; // Consume the command
                }
            }

            if (commandToSend != null)
            {
                try
                {
                    await connection.SendAsync("BotCommand", commandToSend);
                    _logger.LogInformation($"Sent BotCommand: {commandToSend.Action}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error sending BotCommand: {ex.Message}");
                }
            }
            await Task.Delay(10); // Small delay to prevent tight loop if no command ready
        }
        _logger.LogInformation("Disconnected or connection failed. Exiting application.");
    }
}
