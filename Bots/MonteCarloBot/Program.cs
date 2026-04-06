using Marvijo.Zooscape.Bots.Common.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ClingyHeuroBot2.Heuristics;
using MonteCarloBot.Services;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Utils;
using Serilog;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MonteCarloBot;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables();

        var configuration = builder.Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            var runnerIp = Environment.GetEnvironmentVariable("RUNNER_IPV4") ?? configuration["RunnerIP"];
            var runnerPort = Environment.GetEnvironmentVariable("RUNNER_PORT") ?? configuration["RunnerPort"];
            var botNickname = Environment.GetEnvironmentVariable("BOT_NICKNAME") ?? configuration["BotNickname"] ?? "MonteCarloBot";
            var hubName = Environment.GetEnvironmentVariable("HUB_NAME") ?? configuration["HubName"];
            var botToken = Environment.GetEnvironmentVariable("Token") ?? configuration["BotToken"] ?? Guid.NewGuid().ToString();

            if (string.IsNullOrWhiteSpace(runnerIp)
                || string.IsNullOrWhiteSpace(runnerPort)
                || string.IsNullOrWhiteSpace(botNickname)
                || string.IsNullOrWhiteSpace(hubName))
            {
                Log.Error("Missing bot configuration.");
                return;
            }

            if (!runnerIp.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !runnerIp.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                runnerIp = $"http://{runnerIp}";
            }

            var url = $"{runnerIp}:{runnerPort}/{hubName}";
            var connection = new HubConnectionBuilder()
                .WithUrl(url)
                .ConfigureLogging(logging =>
                {
                    logging.SetMinimumLevel(LogLevel.Information);
                    logging.AddConsole();
                })
                .WithAutomaticReconnect()
                .Build();

            BootstrapFallbackAssets();
            var botService = new MonteCarloBotService(Log.Logger);
            BotCommand? command = null;
            var commandTick = -1;
            var commandSourceX = -1;
            var commandSourceY = -1;
            var commandSourceCapturedCounter = -1;
            BotAction? lastSentAction = null;
            var lastSentTick = -1;
            var lastSentSourceX = -1;
            var lastSentSourceY = -1;
            var lastSentCapturedCounter = -1;
            var lastAnimalX = -1;
            var lastAnimalY = -1;
            var shouldSendCommand = true;
            var currentTick = -1;

            connection.On<Guid>("Registered", botService.SetBotId);
            connection.On<GameState>("GameState", state =>
            {
                var nextTick = state?.Tick ?? -1;
                currentTick = nextTick;

                if (state is null)
                {
                    command = new BotCommand { Action = BotAction.Up };
                    commandTick = nextTick;
                    commandSourceX = -1;
                    commandSourceY = -1;
                    commandSourceCapturedCounter = -1;
                    shouldSendCommand = true;
                    return;
                }

                try
                {
                    var nextCommand = botService.ProcessState(state) ?? new BotCommand { Action = BotAction.Up };
                    var nextShouldSendCommand = true;

                    var myAnimal = state.Animals?.FirstOrDefault(a => a.Id == botService.BotId);
                    var shouldForceSpawnEscapeCommand = myAnimal is not null
                        && myAnimal.CapturedCounter > 0
                        && myAnimal.X == myAnimal.SpawnX
                        && myAnimal.Y == myAnimal.SpawnY;
                    if (myAnimal is not null
                        && !shouldForceSpawnEscapeCommand
                        && lastSentAction.HasValue
                        && nextCommand.Action == lastSentAction.Value
                        && nextCommand.Action is >= BotAction.Up and <= BotAction.Right
                        && myAnimal.CapturedCounter == lastSentCapturedCounter)
                    {
                        var sameObservedPosition = myAnimal.X == lastSentSourceX && myAnimal.Y == lastSentSourceY;
                        var advancedAlongAction = !(myAnimal.X == lastAnimalX && myAnimal.Y == lastAnimalY);
                        if (sameObservedPosition)
                        {
                            nextShouldSendCommand = false;
                        }
                        else if (advancedAlongAction)
                        {
                            var (nextX, nextY) = BotUtils.ApplyMove(myAnimal.X, myAnimal.Y, nextCommand.Action);
                            if (BotUtils.IsTraversable(state, nextX, nextY))
                            {
                                nextShouldSendCommand = false;
                            }
                        }
                    }

                    command = nextCommand;
                    commandTick = nextTick;
                    shouldSendCommand = nextShouldSendCommand;
                    commandSourceX = myAnimal?.X ?? -1;
                    commandSourceY = myAnimal?.Y ?? -1;
                    commandSourceCapturedCounter = myAnimal?.CapturedCounter ?? -1;

                    if (myAnimal is not null)
                    {
                        lastAnimalX = myAnimal.X;
                        lastAnimalY = myAnimal.Y;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "MonteCarloBot failed to process tick {Tick}", state?.Tick ?? -1);
                    command = new BotCommand { Action = Marvijo.Zooscape.Bots.Common.Enums.BotAction.Up };
                    commandTick = nextTick;
                    commandSourceX = -1;
                    commandSourceY = -1;
                    commandSourceCapturedCounter = -1;
                    shouldSendCommand = true;
                }
            });

            connection.On<string>("Disconnect", async reason =>
            {
                Log.Information("Disconnected: {Reason}", reason);
                await connection.StopAsync();
            });

            connection.Closed += async error =>
            {
                Log.Error(error, "Connection closed");
                await Task.Delay(1000);
                try
                {
                    await connection.StartAsync();
                    await connection.InvokeAsync("Register", botToken, botNickname);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Reconnect failed");
                }
            };

            await connection.StartAsync();
            await connection.InvokeAsync("Register", botToken, botNickname);

            while (connection.State is HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting)
            {
                if (command is not null
                    && commandTick > lastSentTick
                    && command.Action is >= BotAction.Up and <= BotAction.UseItem
                    && shouldSendCommand)
                {
                    try
                    {
                        await connection.SendAsync("BotCommand", command);
                        lastSentAction = command.Action;
                        lastSentTick = commandTick;
                        lastSentSourceX = commandSourceX;
                        lastSentSourceY = commandSourceY;
                        lastSentCapturedCounter = commandSourceCapturedCounter;
                        shouldSendCommand = false;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "MonteCarloBot failed to send command for tick {Tick}", currentTick);
                    }
                }

                await Task.Delay(10);
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "MonteCarloBot terminated unexpectedly.");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void BootstrapFallbackAssets()
    {
        try
        {
            var repoRoot = FindRepoRoot();
            if (repoRoot is null)
            {
                Log.Warning("Could not locate repo root from {BaseDirectory}; fallback bootstrap skipped.", AppContext.BaseDirectory);
                return;
            }

            var runtimeDir = AppContext.BaseDirectory;
            var fallbackDir = Path.Combine(repoRoot, "Bots", "ClingyHeuroBot2");
            var runtimeEvolutionDir = Path.Combine(runtimeDir, "evolution-data");

            Directory.CreateDirectory(runtimeEvolutionDir);

            CopyIfNewer(
                Path.Combine(fallbackDir, "best-individuals.json"),
                Path.Combine(runtimeDir, "best-individuals.json"));

            CopyIfNewer(
                Path.Combine(fallbackDir, "heuristic-weights.json"),
                Path.Combine(runtimeDir, "heuristic-weights.json"));

            EnsureJsonArrayFile(
                Path.Combine(runtimeEvolutionDir, "high-scores.json"),
                Path.Combine(fallbackDir, "evolution-data", "high-scores.json"));

            SeedFallbackStaticWeights(
                Path.Combine(fallbackDir, "heuristic-weights.json"));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to bootstrap Clingy fallback assets.");
        }
    }

    private static string? FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Marvijo.Zooscape.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static void CopyIfNewer(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        var shouldCopy = !File.Exists(destinationPath)
            || File.GetLastWriteTimeUtc(sourcePath) > File.GetLastWriteTimeUtc(destinationPath)
            || new FileInfo(sourcePath).Length != new FileInfo(destinationPath).Length;

        if (!shouldCopy)
        {
            return;
        }

        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        File.Copy(sourcePath, destinationPath, overwrite: true);
        Log.Information("Bootstrapped fallback asset {AssetName} into runtime directory.", Path.GetFileName(destinationPath));
    }

    private static void EnsureJsonArrayFile(string destinationPath, string sourcePath)
    {
        if (IsJsonArrayFile(destinationPath))
        {
            return;
        }

        if (IsJsonArrayFile(sourcePath))
        {
            CopyIfNewer(sourcePath, destinationPath);
            return;
        }

        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        File.WriteAllText(destinationPath, "[]");
        Log.Information("Initialized fallback runtime file {FileName} with an empty JSON array.", Path.GetFileName(destinationPath));
    }

    private static bool IsJsonArrayFile(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var firstContent = File.ReadAllText(path).TrimStart();
            return firstContent.StartsWith("[", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static void SeedFallbackStaticWeights(string weightsPath)
    {
        if (!File.Exists(weightsPath))
        {
            return;
        }

        var weightsJson = File.ReadAllText(weightsPath);
        var weights = JsonSerializer.Deserialize<HeuristicWeights>(weightsJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        });

        if (weights is null)
        {
            return;
        }

        var weightManagerType = typeof(WeightManager);
        weightManagerType.GetField("_staticWeights", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, weights);
        weightManagerType.GetField("_cachedWeights", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, null);
        weightManagerType.GetField("_cachedIndividualId", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, null);
        weightManagerType.GetField("_lastWeightsUpdate", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, DateTime.MinValue);

        Log.Information("Seeded Clingy fallback static weights from {WeightsPath}.", weightsPath);
    }
}
