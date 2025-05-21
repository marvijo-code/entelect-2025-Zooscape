using System;
using System.Text.Json;
using DeepMCTS.Enums; // Added for BotAction
using DeepMCTS.Models; // Added for GameState, BotCommand
using DeepMCTS.Services; // Changed from MCTSo4.Services
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

// Configure Serilog logger from appsettings.json
var builder = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

var configuration = builder.Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .Enrich.FromLogContext()
    .WriteTo.File(
        "deepmcts-logs/deepmcts-.txt", // Changed log file path
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7
    )
    .CreateLogger();

// BotService? logic = null; // Removed unused variable

try
{
    Log.Information("DeepMCTS Bot starting up..."); // Changed bot name

    var runnerIpConfig =
        Environment.GetEnvironmentVariable("RUNNER_IPV4") ?? configuration["RunnerIP"];
    var runnerPortConfig =
        Environment.GetEnvironmentVariable("RUNNER_PORT") ?? configuration["RunnerPort"];
    var botNickname =
        Environment.GetEnvironmentVariable("BOT_NICKNAME")
        ?? configuration["BotNickname"]
        ?? "DeepMCTS_Bot"; // Default nickname
    Log.Information($"Bot Nickname from config: {botNickname}");
    var hubName = Environment.GetEnvironmentVariable("HUB_NAME") ?? configuration["HubName"];
    var botToken =
        Environment.GetEnvironmentVariable("Token")
        ?? configuration["BotToken"]
        ?? Guid.NewGuid().ToString();

    Log.Debug("RUNNER_IPV4: {RunnerIPV4}", Environment.GetEnvironmentVariable("RUNNER_IPV4"));
    Log.Debug("RUNNER_PORT: {RunnerPort}", Environment.GetEnvironmentVariable("RUNNER_PORT"));
    Log.Debug("BOT_NICKNAME: {BotNicknameEnv}", Environment.GetEnvironmentVariable("BOT_NICKNAME"));
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

    var connection = new HubConnectionBuilder()
        .WithUrl(connectionUrl)
        .ConfigureLogging(logging =>
        {
            logging.AddSerilog();
            logging.SetMinimumLevel(LogLevel.Information);
        })
        .WithAutomaticReconnect()
        .Build();

    // The BotService from the prompt doesn't have a constructor expecting HubConnection
    // nor does it have StartAsync. The original MCTSo4Logic likely handled SignalR communication.
    // The new BotService only has ProcessState. This needs to be adapted.
    // For now, I'll assume a similar structure to MCTSo4Logic is needed for DeepMCTS to integrate.
    // This will require creating a class similar to MCTSo4Logic that uses the new BotService.
    // Let's call it DeepMctsSignalRHandler for now, and create it later.

    // logic = new BotService(connection); // This line would be incorrect with the current BotService
    // Placeholder for where the SignalR handling logic would be initialized
    // For the BotService provided, it doesn't manage the connection itself.
    // A wrapper class will be needed.

    // Let's assume we'll have a `DeepMctsGameHandler` that wraps `BotService` and handles SignalR calls.
    // This is a temporary simplification; `BotService` itself doesn't have `StartAsync` or handle `GameState` from SignalR.
    // The original prompt for BotService only includes `ProcessState`.
    // We'll need to define how GameState is received and BotCommand is sent.

    // For now, let's instantiate BotService directly but note its methods won't be called by this Program.cs yet
    // without a SignalR handling wrapper.
    var botServiceInstance = new BotService(); // Instantiate it, though it's not fully integrated with SignalR here.

    // The original `logic.StartAsync` and event handlers would need to be adapted for a class that
    // uses `botServiceInstance.ProcessState` when a game state is received.

    // Simulating the connection logic for now, but this needs a proper handler class.
    connection.On<GameState>(
        "GameState",
        async (gameState) =>
        {
            Log.Information("Received game state for tick {Tick}", gameState.Tick);
            if (gameState.MyAnimalId == Guid.Empty && botServiceInstance.GetMyBotId() != Guid.Empty)
            {
                gameState.MyAnimalId = botServiceInstance.GetMyBotId();
            }

            try
            {
                BotCommand command = botServiceInstance.ProcessState(gameState);
                await connection.SendAsync("BotCommand", command);
                Log.Information("Sent command: {Action}", command.Action);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing game state or sending command.");
            }
        }
    );

    connection.On<string>(
        "ReceiveMessage",
        (message) =>
        {
            Log.Information("Message from server: {Message}", message);
        }
    );

    connection.On<Guid>(
        "Registered",
        (botIdFromServer) =>
        {
            Log.Information(
                "Successfully registered with the game runner. Bot ID from server: {BotId}",
                botIdFromServer
            );
            botServiceInstance.SetOfficialBotId(botIdFromServer);
        }
    );

    connection.On<string>(
        "Disconnect",
        async (reason) =>
        {
            Log.Warning(
                "Disconnected by server. Reason: {Reason}. Connection will be closed.",
                reason
            );
            await Task.CompletedTask;
        }
    );

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
            await connection.InvokeAsync("Register", botToken, botNickname);
            Log.Information(
                "Successfully re-registered with token: {BotToken} and nickname: {BotNickname}",
                botToken,
                botNickname
            );
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Reconnection or re-registration failed: {ErrorMessage}", ex.Message);
        }
    };

    await connection.StartAsync();
    Log.Information("Connection started successfully.");

    await connection.InvokeAsync("Register", botToken, botNickname);
    Log.Information(
        "Successfully registered with token: {BotToken} and nickname: {BotNickname}",
        botToken,
        botNickname
    );

    while (true)
    {
        if (connection.State == HubConnectionState.Disconnected)
        {
            Log.Warning(
                "Connection disconnected, program will attempt to handle via Closed event."
            );
        }
        await Task.Delay(5000);
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "DeepMCTS Bot terminated unexpectedly.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
