using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace HeuroBot
{
    public class SignalRBotClient
    {
        private readonly HubConnection connection;
        private readonly string botId;
        private readonly ILogger<SignalRBotClient> logger;
        private readonly RLBotService rlBotService;
        private readonly string modelPath;
        private bool isConnected = false;
        private int gameCount = 0;
        private int totalMoves = 0;
        private int totalPelletsCollected = 0;
        private int totalCaptures = 0;
        private List<double> processingTimes = new List<double>();

        public SignalRBotClient(string engineUrl, string botId, string modelPath = null)
        {
            this.botId = botId;
            this.modelPath = modelPath;

            // Setup logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.AddFile("logs/rl_bot.log");
            });
            this.logger = loggerFactory.CreateLogger<SignalRBotClient>();

            // Initialize RL bot service
            this.rlBotService = new RLBotService(modelPath);

            // Create SignalR connection
            connection = new HubConnectionBuilder()
                .WithUrl(engineUrl)
                .WithAutomaticReconnect()
                .ConfigureLogging(logging =>
                {
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Information);
                })
                .Build();

            // Register handlers
            RegisterHandlers();
        }

        private void RegisterHandlers()
        {
            // Register handler for receiving game state
            connection.On<GameState>(
                "ReceiveGameState",
                async (gameState) =>
                {
                    try
                    {
                        // Measure processing time
                        var stopwatch = Stopwatch.StartNew();

                        // Process game state and get next action
                        BotAction action = rlBotService.GetNextAction(gameState);
                        totalMoves++;

                        // Create command
                        BotCommand command = new BotCommand { BotId = botId, Action = action };

                        // Send command back to engine
                        await connection.InvokeAsync("SendBotCommand", command);

                        // Log processing time
                        stopwatch.Stop();
                        double processingTime = stopwatch.Elapsed.TotalMilliseconds;
                        processingTimes.Add(processingTime);

                        if (processingTime > 130)
                        {
                            logger.LogWarning(
                                $"Processing time: {processingTime}ms (approaching 150ms limit)"
                            );
                        }
                        else if (processingTime > 100)
                        {
                            logger.LogInformation(
                                $"Processing time: {processingTime}ms (moderate)"
                            );
                        }

                        // Log every 100 moves
                        if (totalMoves % 100 == 0)
                        {
                            LogPerformanceStats();
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Error processing game state: {ex.Message}");
                        logger.LogError(ex.StackTrace);

                        // Try to send a fallback command in case of error
                        try
                        {
                            // Use a simple fallback action
                            BotCommand fallbackCommand = new BotCommand
                            {
                                BotId = botId,
                                Action = BotAction.Up, // Default fallback
                            };

                            await connection.InvokeAsync("SendBotCommand", fallbackCommand);
                            logger.LogWarning("Sent fallback command due to error");
                        }
                        catch (Exception fallbackEx)
                        {
                            logger.LogError(
                                $"Failed to send fallback command: {fallbackEx.Message}"
                            );
                        }
                    }
                }
            );

            // Register handler for game over
            connection.On(
                "GameOver",
                () =>
                {
                    gameCount++;
                    logger.LogInformation($"Game {gameCount} over. Total moves: {totalMoves}");

                    // Get stats from RL service
                    int pelletsCollected = rlBotService.GetPelletsCollected();
                    int captures = rlBotService.GetCaptures();
                    totalPelletsCollected += pelletsCollected;
                    totalCaptures += captures;

                    logger.LogInformation(
                        $"Game stats - Pellets: {pelletsCollected}, Captures: {captures}"
                    );

                    // Log overall performance
                    LogPerformanceStats();

                    // Reset the RL service for the next game
                    rlBotService.Reset();

                    // Save the model after each game
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string savePath = Path.Combine(
                        "models",
                        $"rl_bot_game_{gameCount}_{timestamp}.weights.h5"
                    );
                    rlBotService.SaveModel(savePath);
                }
            );

            // Register handler for connection closed
            connection.Closed += async (error) =>
            {
                isConnected = false;
                logger.LogWarning($"Connection closed: {error?.Message}");

                // Try to reconnect
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await ConnectAsync();
            };
        }

        private void LogPerformanceStats()
        {
            if (processingTimes.Count == 0)
                return;

            double avgTime = processingTimes.Average();
            double maxTime = processingTimes.Max();
            double minTime = processingTimes.Min();
            double p95Time = processingTimes
                .OrderBy(t => t)
                .ElementAt((int)(processingTimes.Count * 0.95));
            double under150ms =
                processingTimes.Count(t => t < 150) / (double)processingTimes.Count * 100;

            logger.LogInformation($"Performance stats after {totalMoves} moves:");
            logger.LogInformation(
                $"  Avg time: {avgTime:F2}ms, Max: {maxTime:F2}ms, Min: {minTime:F2}ms, P95: {p95Time:F2}ms"
            );
            logger.LogInformation($"  Under 150ms: {under150ms:F2}%");
            logger.LogInformation(
                $"  Total games: {gameCount}, Pellets: {totalPelletsCollected}, Captures: {totalCaptures}"
            );

            // Clear the list if it gets too large
            if (processingTimes.Count > 10000)
            {
                processingTimes.Clear();
            }
        }

        public async Task ConnectAsync()
        {
            if (isConnected)
                return;

            try
            {
                await connection.StartAsync();
                isConnected = true;
                logger.LogInformation("Connected to engine");

                // Register bot with engine
                await connection.InvokeAsync("RegisterBot", botId);
                logger.LogInformation($"Registered bot with ID: {botId}");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error connecting to engine: {ex.Message}");
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            if (!isConnected)
                return;

            try
            {
                await connection.StopAsync();
                isConnected = false;
                logger.LogInformation("Disconnected from engine");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error disconnecting from engine: {ex.Message}");
            }
        }

        public bool IsConnected => isConnected;
    }

    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Parse command line arguments
            string engineUrl = "http://localhost:5000/gameHub";
            string botId = "RLBot";
            string modelPath = null;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--url" && i + 1 < args.Length)
                {
                    engineUrl = args[i + 1];
                    i++;
                }
                else if (args[i] == "--botId" && i + 1 < args.Length)
                {
                    botId = args[i + 1];
                    i++;
                }
                else if (args[i] == "--model" && i + 1 < args.Length)
                {
                    modelPath = args[i + 1];
                    i++;
                }
            }

            Console.WriteLine($"Starting RL bot with ID: {botId}");
            Console.WriteLine($"Connecting to engine at: {engineUrl}");
            if (!string.IsNullOrEmpty(modelPath))
            {
                Console.WriteLine($"Using model: {modelPath}");
            }

            // Create directories for models and logs
            Directory.CreateDirectory("models");
            Directory.CreateDirectory("logs");

            // Create SignalR client
            var client = new SignalRBotClient(engineUrl, botId, modelPath);

            try
            {
                // Connect to engine
                await client.ConnectAsync();

                // Keep the program running until manually terminated
                Console.WriteLine("Bot is running. Press Ctrl+C to exit.");
                var exitEvent = new ManualResetEvent(false);
                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    exitEvent.Set();
                };

                exitEvent.WaitOne();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                // Disconnect from engine
                if (client.IsConnected)
                {
                    await client.DisconnectAsync();
                }
            }
        }
    }
}
