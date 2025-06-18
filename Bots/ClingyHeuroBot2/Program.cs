using ClingyHeuroBot2.Services;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Net.Sockets;

namespace HeuroBot;

public class Program
{
    public static IConfigurationRoot? Configuration;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("TEST: Program.Main has started.");
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        Log.Information("TEST: Serilog configured and attempting to log to console.");

        try
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
        Log.Information($"Connecting to {url}");
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

        var botService = new HeuroBotService(Log.Logger);
        BotCommand? command = null;

        connection.On<Guid>("Registered", id => botService.SetBotId(id));
        connection.On<GameState>("GameState", state => command = botService.ProcessState(state));
        connection.On<string>(
            "Disconnect",
            async reason =>
            {
                Log.Information($"Disconnected: {reason}");
                await connection.StopAsync();
            }
        );
        connection.Closed += async error =>
        {
            Log.Error($"Connection closed: {error?.Message}");
            await Task.Delay(new Random().Next(0, 5) * 1000);
            try
            {
                await connection.StartAsync();
                await connection.InvokeAsync("Register", botToken, botNickname);
            }
            catch (Exception ex)
            {
                Log.Error($"Reconnection failed: {ex.Message}");
            }
        };

        try
        {
            await connection.StartAsync();
            await connection.InvokeAsync("Register", botToken, botNickname);
        }
        catch (HttpRequestException httpEx) when (httpEx.InnerException is SocketException sockEx && sockEx.SocketErrorCode == SocketError.ConnectionRefused)
        {
            Log.Error("Connection failed: The target machine actively refused it.");
            return;
        }
        catch (Exception ex)
        {
            Log.Error($"Startup error: {ex.GetBaseException().Message}");
            return;
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
                Log.Information($"Sent BotCommand: {command.Action}");
            }
            command = null;
            await Task.Delay(100);
        }
        Console.WriteLine("TEST: End of try block reached. Press Enter to exit.");
        Console.ReadLine();
        } // End of try
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application start-up failed");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
