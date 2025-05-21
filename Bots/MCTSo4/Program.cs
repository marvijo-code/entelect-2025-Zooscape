using MCTSo4.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
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
        "mctso4-logs/mctso4-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7
    )
    .CreateLogger();

MCTSo4Logic? logic = null;

try
{
    Log.Information("MCTSo4 Bot starting up...");

    var runnerIpConfig =
        Environment.GetEnvironmentVariable("RUNNER_IPV4") ?? configuration["RunnerIP"];
    var runnerPortConfig =
        Environment.GetEnvironmentVariable("RUNNER_PORT") ?? configuration["RunnerPort"];
    var botNickname =
        Environment.GetEnvironmentVariable("BOT_NICKNAME") ?? configuration["BotNickname"];
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

    logic = new MCTSo4Logic(connection);

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
            if (logic != null)
            {
                await logic.StartAsync(botToken, botNickname);
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

    await connection.StartAsync();
    Log.Information("Connection started successfully.");

    if (logic != null)
    {
        await logic.StartAsync(botToken, botNickname);
    }
    else
    {
        Log.Error("Logic service not initialized at startup.");
        return;
    }

    // Keep the application running by monitoring the connection state
    while (true)
    {
        if (connection.State == HubConnectionState.Disconnected)
        {
            Log.Warning("Connection disconnected, attempting to reconnect...");
            try
            {
                await connection.StartAsync();
                // Re-register after reconnection
                if (logic != null)
                {
                    await logic.StartAsync(botToken, botNickname);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to reconnect: {ErrorMessage}", ex.Message);
            }
        }
        await Task.Delay(1000); // Check connection state every second
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "MCTSo4 Bot terminated unexpectedly.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
