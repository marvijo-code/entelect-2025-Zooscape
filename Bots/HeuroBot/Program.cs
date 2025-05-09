using HeuroBot.Models;
using HeuroBot.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HeuroBot;

public class Program
{
    public static IConfigurationRoot? Configuration;
    private static readonly ILogger _logger = new LoggerFactory().CreateLogger<Program>();

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
        var hubName = Environment.GetEnvironmentVariable("HUB_NAME") ?? Configuration["HubName"];
        var botToken =
            Environment.GetEnvironmentVariable("BOT_TOKEN")
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

        string url = $"{runnerIp}:{runnerPort}/{hubName}";
        _logger.LogInformation($"Connecting to {url}");
        Console.WriteLine($"Connecting to {url}");

        var connection = new HubConnectionBuilder()
            .WithUrl(url)
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Debug);
                logging.AddConsole();
            })
            .WithAutomaticReconnect()
            .Build();

        var botService = new HeuroBotService();
        BotCommand command = new();

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

        try
        {
            await connection.StartAsync();
            await connection.InvokeAsync("Register", botToken, botNickname);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Startup error: {ex.Message}");
            return;
        }

        while (
            connection.State == HubConnectionState.Connected
            || connection.State == HubConnectionState.Connecting
        )
        {
            if (
                command != null
                && command.Action >= Enums.BotAction.Up
                && command.Action <= Enums.BotAction.Right
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
