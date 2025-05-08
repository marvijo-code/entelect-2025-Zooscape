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

    private static async Task Main(string[] args)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory) // Ensures appsettings.json is found in deployed/published scenarios
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables();

        Configuration = builder.Build();

        // Read settings from configuration
        var runnerIpConfig = Configuration.GetSection("BotSettings:EngineUrl").Value;
        var botNickname = Configuration.GetSection("BotSettings:PlayerId").Value ?? "MCTS_Bot";
        var botToken =
            Configuration.GetSection("BotSettings:PlayerKey").Value ?? Guid.NewGuid().ToString();
        var mctsIterations = Configuration.GetValue<int>("BotSettings:MCTSIterations", 1000);
        var mctsExploration = Configuration.GetValue<double>(
            "BotSettings:MCTSExplorationParam",
            1.414
        );

        // Override with environment variables if available
        var envRunnerIp = Environment.GetEnvironmentVariable("RUNNER_IPV4_OR_URL");
        if (!string.IsNullOrWhiteSpace(envRunnerIp))
        {
            runnerIpConfig = envRunnerIp.StartsWith("http") ? envRunnerIp : "http://" + envRunnerIp;
        }

        var envBotNickname = Environment.GetEnvironmentVariable("BOT_NICKNAME");
        if (!string.IsNullOrWhiteSpace(envBotNickname))
            botNickname = envBotNickname;

        var envBotToken = Environment.GetEnvironmentVariable("BOT_TOKEN");
        if (!string.IsNullOrWhiteSpace(envBotToken))
            botToken = envBotToken;

        if (string.IsNullOrWhiteSpace(runnerIpConfig))
        {
            runnerIpConfig = "http://localhost:5000/runnerhub"; // Default value
            Console.WriteLine(
                $"Warning: Engine URL not configured via appsettings.json (BotSettings:EngineUrl) or RUNNER_IPV4_OR_URL environment variable. Using default: {runnerIpConfig}"
            );
        }

        Console.WriteLine($"Connecting to Engine at: {runnerIpConfig}");
        Console.WriteLine($"Bot Nickname: {botNickname}");
        Console.WriteLine($"MCTS Iterations: {mctsIterations}, Exploration: {mctsExploration}");

        var connection = new HubConnectionBuilder()
            .WithUrl(runnerIpConfig) // Expects full URL like http://localhost:5000/runnerhub
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
                Console.WriteLine($"Successfully registered with ID: {id}");
                botLogic.SetBotId(id);
            }
        );

        connection.On<EngineGameState>(
            "GameState",
            (engineGameState) =>
            {
                Console.WriteLine($"Received GameState for Tick: {engineGameState?.Tick}");
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
                Console.WriteLine($"Server sent disconnect with reason: {reason}");
                await connection.StopAsync();
            }
        );

        connection.Closed += (error) =>
        {
            Console.WriteLine($"Connection closed. Error: {error?.Message}");
            return Task.CompletedTask;
        };

        try
        {
            await connection.StartAsync();
            Console.WriteLine("Connection started. Attempting to register...");

            await connection.InvokeAsync("Register", botToken, botNickname);
            Console.WriteLine("Register message sent.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting connection or registering: {ex.Message}");
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
                    Console.WriteLine($"Sent BotCommand: {commandToSend.Action}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending BotCommand: {ex.Message}");
                }
            }
            await Task.Delay(10); // Small delay to prevent tight loop if no command ready
        }
        Console.WriteLine("Disconnected or connection failed. Exiting application.");
    }
}
