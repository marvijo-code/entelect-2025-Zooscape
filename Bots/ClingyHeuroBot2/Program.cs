using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;

using ClingyHeuroBot2;
using ClingyHeuroBot2.Heuristics;
using ClingyHeuroBot2.Services;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;

namespace HeuroBot;

public class Program
{
    public static IConfigurationRoot? Configuration;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("TEST: Program.Main has started.");

        // Configure Serilog first
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateLogger();

        Log.Information("TEST: Serilog configured and attempting to log to console.");

        // Check if we should just export best individuals
        if (args.Length > 0 && args[0] == "--export-best")
        {
            await ExportBestIndividuals();
            return;
        }

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

            // Initialize evolution system
            var evolutionCoordinator = EvolutionCoordinator.Instance;
            var individualId = evolutionCoordinator.RegisterBot(botNickname);
            Log.Information($"Bot '{botNickname}' registered with individual ID: {individualId}");

            var botService = new HeuroBotService(Log.Logger);
            BotCommand? command = null;

            // Timing variables for performance monitoring (per-tick stopwatch will be local)
            DateTime? gameStateReceivedTime = null;
            int currentTick = -1; // Track current game tick

            // Game tracking for evolution
            DateTime gameStartTime = DateTime.UtcNow;
            int initialScore = 0;
            int finalScore = 0;
            int currentRank = 1;
            int totalPlayers = 1;
            int lastRecordedScore = 0;
            int ticksPlayed = 0;
            DateTime lastPerformanceRecord = DateTime.UtcNow;

            connection.On<Guid>("Registered", id => botService.SetBotId(id));
            connection.On<GameState>("GameState", async state =>
            {
                // Add comprehensive null checks for GameState
                if (state == null)
                {
                    Log.Error("Received null GameState from SignalR connection");
                    command = new BotCommand { Action = BotAction.Up }; // Safe fallback
                    return;
                }

                // Validate critical GameState properties
                if (state.Animals == null)
                {
                    Log.Error("Received GameState with null Animals collection for tick {Tick}", state.Tick);
                    command = new BotCommand { Action = BotAction.Up }; // Safe fallback
                    return;
                }

                if (state.Cells == null)
                {
                    Log.Error("Received GameState with null Cells collection for tick {Tick}. Animals count: {AnimalsCount}",
                        state.Tick, state.Animals?.Count ?? -1);
                    command = new BotCommand { Action = BotAction.Up }; // Safe fallback
                    return;
                }

                // Check if botService itself is null
                if (botService == null)
                {
                    Log.Error("BotService is null! This should never happen. Creating emergency fallback.");
                    command = new BotCommand { Action = BotAction.Up }; // Safe fallback
                    return;
                }

                // Start timing when game state is received
                gameStateReceivedTime = DateTime.UtcNow;
                var tickStopwatch = Stopwatch.StartNew();
                currentTick = state.Tick; // Capture current tick

                // Declare myAnimal outside try block so it can be used later
                Animal? myAnimal = null;

                try
                {
                    myAnimal = state.Animals.FirstOrDefault(a => a.Id == botService.BotId);
                    if (myAnimal == null && botService.BotId != Guid.Empty)
                    {
                        Log.Warning("Bot animal with ID {BotId} not found in GameState for tick {Tick}. Available animals: [{AvailableAnimals}]. Processing anyway with default action.",
                            botService.BotId, currentTick, string.Join(", ", state.Animals.Select(a => a.Id.ToString())));
                        command = new BotCommand { Action = BotAction.Up }; // Safe fallback
                        return;
                    }

                    command = botService.ProcessState(state);

                    // Ensure command is not null
                    if (command == null)
                    {
                        Log.Warning("BotService.ProcessState returned null command for tick {Tick}. Using fallback.", currentTick);
                        command = new BotCommand { Action = BotAction.Up };
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error processing GameState for tick {Tick}. BotId: {BotId}, Animals count: {AnimalsCount}, Cells count: {CellsCount}. Exception: {ExceptionType}: {ExceptionMessage}",
                        currentTick, botService?.BotId ?? Guid.Empty, state.Animals?.Count ?? -1, state.Cells?.Count ?? -1, ex.GetType().Name, ex.Message);

                    // Provide detailed context for NullReferenceExceptions in a single structured log entry
                    if (ex is NullReferenceException)
                    {
                        Log.Error(ex,
                            "NullReferenceException caught at tick {Tick}. BotServiceNull={BotServiceNull}, BotId={BotId}, WeightManagerNull={WeightManagerNull}",
                            currentTick,
                            botService is null,
                            botService?.BotId ?? Guid.Empty,
                            WeightManager.Instance is null);
                    }

                    command = new BotCommand { Action = BotAction.Up }; // Default fallback action
                }

                // Log processing time immediately after getting the command
                tickStopwatch.Stop();
                var processingTimeMs = tickStopwatch.ElapsedMilliseconds;

                // Get current position for logging (using existing myAnimal)
                var position = myAnimal != null ? $"({myAnimal.X},{myAnimal.Y})" : "(?,?)";
                var score = myAnimal?.Score ?? 0;

                // Concise tick logging: Tick, Position, Action, Duration
                Log.Information("T{Tick} {Position} {Action} {Duration}ms {Score}pts",
                    currentTick, position, command?.Action ?? BotAction.Up, processingTimeMs, score);

                // Warn if processing took longer than expected (170ms threshold)
                if (processingTimeMs > 170)
                {
                    Log.Warning("SLOW T{Tick} {Position} {Action} {Duration}ms - Performance issue!",
                        currentTick, position, command?.Action ?? BotAction.Up, processingTimeMs);
                }

                // Track game performance for evolution
                try
                {
                    ticksPlayed++; // Count total ticks played

                    // Only track performance if bot is registered and has valid ID
                    var botId = botService?.BotId ?? Guid.Empty;
                    if (botId != Guid.Empty && state.Animals != null)
                    {
                        // Reuse existing myAnimal instead of re-querying
                        if (myAnimal == null)
                        {
                            myAnimal = state.Animals.FirstOrDefault(a => a.Id == botId);
                        }

                        if (myAnimal != null)
                        {
                            finalScore = myAnimal.Score;
                            totalPlayers = state.Animals.Count;

                            // Calculate rank (1 = best)
                            var sortedByScore = state.Animals.OrderByDescending(a => a.Score).ToList();
                            currentRank = sortedByScore.FindIndex(a => a.Id == botService.BotId) + 1;

                            // Reset game start time if this is a new game (score reset to 0 or decreased significantly)
                            if (myAnimal.Score == 0 && lastRecordedScore > 0)
                            {
                                Log.Information("New game detected (score reset to 0). Recording previous game performance and starting fresh.");

                                // Record performance for the previous game
                                try
                                {
                                    var previousGameTime = DateTime.UtcNow - gameStartTime;
                                    if (previousGameTime.TotalSeconds > 30) // Only record if game lasted more than 30 seconds
                                    {
                                        await evolutionCoordinator.RecordPerformanceAsync(botNickname, lastRecordedScore, previousGameTime, currentRank, totalPlayers);
                                        Log.Information($"Previous game performance recorded: Score={lastRecordedScore}, Duration={previousGameTime.TotalSeconds:F1}s");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Warning($"Failed to record previous game performance: {ex.Message}");
                                }

                                // Reset for new game
                                gameStartTime = DateTime.UtcNow;
                                initialScore = myAnimal.Score;
                                ticksPlayed = 0;
                                lastPerformanceRecord = DateTime.UtcNow;
                            }
                            else if (myAnimal.Score < initialScore - 50) // Significant score drop (likely respawned after capture)
                            {
                                gameStartTime = DateTime.UtcNow;
                                initialScore = myAnimal.Score;
                            }

                            lastRecordedScore = myAnimal.Score;

                            // Record performance periodically during long games (every 3 minutes)
                            var timeSinceLastRecord = DateTime.UtcNow - lastPerformanceRecord;
                            if (timeSinceLastRecord.TotalMinutes >= 3 && ticksPlayed > 100)
                            {
                                Log.Information("Recording interim performance after {Minutes:F1} minutes of gameplay", timeSinceLastRecord.TotalMinutes);

                                try
                                {
                                    var currentGameTime = DateTime.UtcNow - gameStartTime;
                                    await evolutionCoordinator.RecordPerformanceAsync(botNickname, finalScore, currentGameTime, currentRank, totalPlayers);
                                    Log.Information($"Interim performance recorded: Score={finalScore}, Rank={currentRank}/{totalPlayers}, Duration={currentGameTime.TotalSeconds:F1}s, Ticks={ticksPlayed}");
                                    lastPerformanceRecord = DateTime.UtcNow;
                                }
                                catch (Exception ex)
                                {
                                    Log.Warning($"Failed to record interim performance: {ex.Message}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"Error tracking game performance: {ex.Message}");
                }
            });
            // Handler for game completion
            connection.On<int, int>("EndGame", async (seed, totalTicks) =>
            {
                Log.Information($"EndGame received: Seed={seed}, TotalTicks={totalTicks}");

                // Record game performance when game ends
                try
                {
                    var gameTime = DateTime.UtcNow - gameStartTime;
                    await evolutionCoordinator.RecordPerformanceAsync(botNickname, finalScore, gameTime, currentRank, totalPlayers);
                    Log.Information($"Game performance recorded: Score={finalScore}, Rank={currentRank}/{totalPlayers}, Time={gameTime.TotalSeconds:F1}s, TotalTicks={totalTicks}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"Failed to record game performance: {ex.Message}");
                }
            });

            connection.On<string>(
                "Disconnect",
                async reason =>
                {
                    Log.Information($"Disconnected: {reason}");

                    // Record game performance before disconnecting (backup in case EndGame wasn't called)
                    try
                    {
                        var gameTime = DateTime.UtcNow - gameStartTime;
                        await evolutionCoordinator.RecordPerformanceAsync(botNickname, finalScore, gameTime, currentRank, totalPlayers);
                        Log.Information($"Game performance recorded on disconnect: Score={finalScore}, Rank={currentRank}/{totalPlayers}, Time={gameTime.TotalSeconds:F1}s");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"Failed to record game performance: {ex.Message}");
                    }

                    await connection.StopAsync();
                }
            );
            connection.Closed += async error =>
            {
                Log.Error($"Connection closed: {error?.Message}");

                // Record performance on unexpected close if a game was in progress
                if (ticksPlayed > 0)
                {
                    try
                    {
                        var gameTime = DateTime.UtcNow - gameStartTime;
                        await evolutionCoordinator.RecordPerformanceAsync(botNickname, finalScore, gameTime, currentRank, totalPlayers);
                        Log.Information($"Game performance recorded on unexpected close: Score={finalScore}, Rank={currentRank}/{totalPlayers}, Time={gameTime.TotalSeconds:F1}s");

                        // Reset game tracking variables
                        gameStartTime = DateTime.UtcNow;
                        initialScore = 0;
                        finalScore = 0;
                        currentRank = 1;
                        totalPlayers = 1;
                        lastRecordedScore = 0;
                        ticksPlayed = 0;
                        lastPerformanceRecord = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"Failed to record performance on close: {ex.Message}");
                    }
                }

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
                    // Calculate total time from game state received to action sent
                    var totalTimeMs = gameStateReceivedTime.HasValue ?
                        (DateTime.UtcNow - gameStateReceivedTime.Value).TotalMilliseconds : -1;

                    await connection.SendAsync("BotCommand", command);
                    Log.Debug($"Sent BotCommand: {command.Action} at {DateTime.UtcNow:HH:mm:ss.fff} for tick {currentTick}");

                    if (totalTimeMs >= 0)
                    {
                        Log.Debug("TOTAL RESPONSE TIME: {TotalTime}ms from GameState received to action sent for tick {Tick}", totalTimeMs, currentTick);

                        // Warn about potentially problematic response times (170ms threshold)
                        if (totalTimeMs > 170)
                        {
                            Log.Warning("TIMEOUT RISK: Total response time {TotalTime}ms exceeds 170ms threshold for tick {Tick}, action: {Action}", totalTimeMs, currentTick, command.Action);
                        }
                        if (totalTimeMs > 250)
                        {
                            Log.Error("CRITICAL TIMEOUT: Total response time {TotalTime}ms exceeds 250ms - bot may be stuck on tick {Tick}, action: {Action}!", totalTimeMs, currentTick, command.Action);
                        }
                    }

                    // Reset timing variables after sending
                    gameStateReceivedTime = null;
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

    /// <summary>
    /// Exports the best individuals from current evolution data
    /// </summary>
    private static async Task ExportBestIndividuals()
    {
        try
        {
            Console.WriteLine("Exporting best individuals from current evolution data...");

            var coordinator = EvolutionCoordinator.Instance;

            // Give the system a moment to initialize
            await Task.Delay(2000);

            var stats = coordinator.GetStatistics();
            Console.WriteLine($"Current generation: {stats.Generation}");
            Console.WriteLine($"Population size: {stats.PopulationStatistics.PopulationSize}");
            Console.WriteLine($"Best fitness: {stats.PopulationStatistics.BestFitness:F2}");

            // Export top 5 individuals
            await coordinator.ExportBestIndividualsAsync(5);

            Console.WriteLine("Export complete!");
            Console.WriteLine("Files created:");
            Console.WriteLine("- best-individuals.json (for git)");
            Console.WriteLine("- best-individuals-genXXXX-timestamp.json (backup)");

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
