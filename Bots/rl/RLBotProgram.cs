using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HeuroBot.Models;
using HeuroBot.Services;
using Microsoft.Extensions.Logging;

namespace HeuroBot
{
    public class RLBotProgram
    {
        private static GameStatePublisher publisher;
        private static Dictionary<string, int> scores = new Dictionary<string, int>();
        private static ILogger<RLBotProgram> logger;

        public static void Main(string[] args)
        {
            // Setup logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });
            logger = loggerFactory.CreateLogger<RLBotProgram>();
            var publisherLogger = loggerFactory.CreateLogger<GameStatePublisher>();

            // Create and start the game state publisher
            publisher = new GameStatePublisher(publisherLogger);
            publisher.Start();

            // Initialize scores
            scores["RLBot"] = 0;
            scores["RefBot"] = 0;

            // Create the RL bot service with TensorFlow integration
            var rlBotService = new RLBotService();

            // Register for game state updates
            GameStateReceived += (gameState) =>
            {
                // Update scores based on pellet collection
                UpdateScores(gameState);

                // Publish game state and scores for visualization
                publisher.PublishGameState(gameState, scores);

                // Process game state with RL model and return action
                return rlBotService.GetNextAction(gameState);
            };

            logger.LogInformation("RL Bot started with visualization. Press Ctrl+C to exit.");

            // Keep the program running until manually terminated
            var exitEvent = new ManualResetEvent(false);
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                exitEvent.Set();
            };

            exitEvent.WaitOne();

            // Cleanup
            publisher.Stop();
            logger.LogInformation("RL Bot shutdown complete.");
        }

        // This would be called by the game engine
        public static event Func<GameState, BotAction> GameStateReceived;

        private static void UpdateScores(GameState gameState)
        {
            // In a real implementation, this would track pellet collection
            // and update scores accordingly

            // For now, we'll just increment scores based on pellets in proximity
            foreach (var animal in gameState.Animals)
            {
                if (animal.BotId == "RLBot" || animal.BotId == "RefBot")
                {
                    // Check surrounding cells for pellets
                    int pelletCount = CountPelletsAround(gameState, animal.Position);
                    if (pelletCount > 0)
                    {
                        // Increment score if pellets are nearby (simulating collection)
                        if (!scores.ContainsKey(animal.BotId))
                            scores[animal.BotId] = 0;

                        scores[animal.BotId] += pelletCount;
                    }
                }
            }
        }

        private static int CountPelletsAround(GameState gameState, Position position)
        {
            int count = 0;
            int[] dx = { -1, 0, 1, 0 };
            int[] dy = { 0, 1, 0, -1 };

            for (int i = 0; i < 4; i++)
            {
                int nx = position.X + dx[i];
                int ny = position.Y + dy[i];

                if (
                    nx >= 0
                    && nx < gameState.Map.Width
                    && ny >= 0
                    && ny < gameState.Map.Height
                    && gameState.Map.Cells[ny, nx].Content == CellContent.Pellet
                )
                {
                    count++;
                }
            }

            return count;
        }
    }
}
