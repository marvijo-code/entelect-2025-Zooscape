using HeuroBotV2.Services;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HeuroBotV2;

public class Program
{
    public static IConfigurationRoot? Configuration;
    private static readonly ILogger _logger = new LoggerFactory().CreateLogger<Program>();
    private const int MaxRetries = 3;

    public static async Task Main(string[] args)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables();

        Configuration = builder.Build();

        var runnerIp =
            Environment.GetEnvironmentVariable("RUNNER_IPV4") ?? Configuration["RunnerIP"];
        var runnerPort =
            Environment.GetEnvironmentVariable("RUNNER_PORT") ?? Configuration["RunnerPort"];
        var botNickname =
            Environment.GetEnvironmentVariable("BOT_NICKNAME") ?? Configuration["BotNickname"];
        Console.WriteLine($"Bot Nickname: {botNickname}");
        var hubName = Environment.GetEnvironmentVariable("HUB_NAME") ?? Configuration["HubName"];
        var botToken =
            Environment.GetEnvironmentVariable("Token")
            ?? Configuration["BotToken"]
            ?? Guid.NewGuid().ToString();

        if (
            string.IsNullOrEmpty(runnerIp)
            || string.IsNullOrEmpty(runnerPort)
            || string.IsNullOrEmpty(botNickname)
            || string.IsNullOrEmpty(hubName)
        )
        {
            Console.WriteLine("Error: Missing configuration.");
            return;
        }

        // Ensure the IP has the http:// scheme, but don't add it if it's already there.
        if (!runnerIp.StartsWith("http://") && !runnerIp.StartsWith("https://"))
        {
            runnerIp = "http://" + runnerIp;
        }
        string url = $"{runnerIp}:{runnerPort}/{hubName}";
        _logger.LogInformation($"Connecting to {url}");
        Console.WriteLine($"Connecting to {url}");

        var connection = new HubConnectionBuilder()
            .WithUrl(url)
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
                logging.AddConsole();
            })
            .WithAutomaticReconnect()
            .Build();

        var botService = new HeuroBotService();
        BotCommand? command = null;

        connection.On<Guid>("Registered", id => botService.SetBotId(id));
        connection.On<GameState>("GameState", state => command = botService.ProcessState(state));
        connection.On<string>(
            "Disconnect",
            async reason =>
            {
                _logger.LogInformation($"Disconnected: {reason}");
                await connection.StopAsync();
            }
        );
        connection.Closed += async error =>
        {
            _logger.LogError($"Connection closed: {error?.Message}");
            await Task.Delay(new Random().Next(0, 5) * 1000);
            try
            {
                await connection.StartAsync();
                await connection.InvokeAsync("Register", botToken, botNickname);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Reconnection failed: {ex.Message}");
            }
        };

        // Connection retry logic with exponential backoff
        bool connected = false;
        for (int attempt = 1; attempt <= MaxRetries && !connected; attempt++)
        {
            try
            {
                _logger.LogInformation($"Connection attempt {attempt}/{MaxRetries}");
                Console.WriteLine($"Connection attempt {attempt}/{MaxRetries}");
                
                await connection.StartAsync();
                await connection.InvokeAsync("Register", botToken, botNickname);
                
                connected = true;
                _logger.LogInformation($"Successfully connected on attempt {attempt}");
                Console.WriteLine($"Successfully connected on attempt {attempt}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Connection attempt {attempt} failed: {ex.Message}");
                Console.WriteLine($"Connection attempt {attempt} failed: {ex.Message}");
                
                if (attempt < MaxRetries)
                {
                    // Exponential backoff: 1s, 2s, 4s
                    int delayMs = (int)Math.Pow(2, attempt - 1) * 1000;
                    _logger.LogInformation($"Waiting {delayMs}ms before retry...");
                    Console.WriteLine($"Waiting {delayMs}ms before retry...");
                    await Task.Delay(delayMs);
                }
                else
                {
                    _logger.LogError($"Failed to connect after {MaxRetries} attempts. Exiting.");
                    Console.WriteLine($"Failed to connect after {MaxRetries} attempts. Exiting.");
                    return;
                }
            }
        }

        while (
            connection.State == HubConnectionState.Connected
            || connection.State == HubConnectionState.Connecting
        )
        {
            if (
                command != null
                && command.Action >= BotAction.Up
                && command.Action <= BotAction.Right
            )
            {
                await connection.SendAsync("BotCommand", command);
                _logger.LogInformation($"Sent BotCommand: {command.Action}");
            }
            command = null;
            await Task.Delay(100);
        }
    }
}
