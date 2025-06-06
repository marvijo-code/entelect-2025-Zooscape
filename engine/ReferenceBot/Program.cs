using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReferenceBot.Models;
using ReferenceBot.Services;

namespace ReferenceBot;

public class Program
{
    public static IConfigurationRoot Configuration;

    private static async Task Main(string[] args)
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile($"appsettings.json", optional: false)
            .AddEnvironmentVariables();

        Configuration = builder.Build();
        var environmentIp = Environment.GetEnvironmentVariable("RUNNER_IPV4");
        var ip = !string.IsNullOrWhiteSpace(environmentIp)
            ? environmentIp
            : Configuration.GetSection("RunnerIP").Value;
        ip = ip.StartsWith("http://") ? ip : "http://" + ip;

        var nickName =
            Environment.GetEnvironmentVariable("BOT_NICKNAME")
            ?? Configuration.GetSection("BotNickname").Value;

        // NOTE: DO NOT generate a random uuid in your own bot, make sure to only use the environment variable.
        var token = Environment.GetEnvironmentVariable("Token") ?? Guid.NewGuid().ToString();

        var port = Configuration.GetSection("RunnerPort");

        var url = ip + ":" + port.Value + "/bothub";

        var connection = new HubConnectionBuilder()
            .WithUrl($"{url}")
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Debug);
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
                Console.WriteLine($"Server sent disconnect with reason: {reason}");
                await connection.StopAsync();
            }
        );

        connection.Closed += (error) =>
        {
            Console.WriteLine($"Server closed with error: {error}");
            return Task.CompletedTask;
        };

        await connection.StartAsync();
        Console.WriteLine("Connected to bot hub");

        await connection.InvokeAsync("Register", token, nickName);
        Console.WriteLine("Sent Register message");

        while (
            connection.State == HubConnectionState.Connected
            || connection.State == HubConnectionState.Connecting
        )
        {
            if (
                botCommand == null
                || botCommand.Action < Enums.BotAction.Up
                || botCommand.Action > Enums.BotAction.UseItem
            )
            {
                continue;
            }
            await connection.SendAsync("BotCommand", botCommand);
            botCommand = null;
        }
    }
}
