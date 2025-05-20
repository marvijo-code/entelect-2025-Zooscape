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
    public class GameStatePublisher
    {
        private TcpListener server;
        private List<TcpClient> clients = new List<TcpClient>();
        private readonly ILogger<GameStatePublisher> logger;
        private bool isRunning = false;
        private readonly int port;

        public GameStatePublisher(ILogger<GameStatePublisher> logger, int port = 5001)
        {
            this.logger = logger;
            this.port = port;
        }

        public void Start()
        {
            if (isRunning)
                return;

            isRunning = true;
            server = new TcpListener(IPAddress.Any, port);

            try
            {
                server.Start();
                logger.LogInformation($"Game state publisher started on port {port}");

                // Start accepting clients in a separate thread
                Task.Run(() => AcceptClientsAsync());
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to start game state publisher: {ex.Message}");
                isRunning = false;
            }
        }

        private async Task AcceptClientsAsync()
        {
            while (isRunning)
            {
                try
                {
                    TcpClient client = await server.AcceptTcpClientAsync();
                    clients.Add(client);
                    logger.LogInformation(
                        $"New visualization client connected. Total clients: {clients.Count}"
                    );
                }
                catch (Exception ex)
                {
                    if (isRunning)
                        logger.LogError($"Error accepting client: {ex.Message}");
                }
            }
        }

        public void PublishGameState(GameState gameState, Dictionary<string, int> scores)
        {
            if (!isRunning || clients.Count == 0)
                return;

            try
            {
                // Convert game state to visualization format
                var visualizationData = ConvertGameStateForVisualization(gameState, scores);
                string jsonData = JsonSerializer.Serialize(visualizationData);
                byte[] data = Encoding.UTF8.GetBytes(jsonData);

                // Send to all connected clients
                List<TcpClient> disconnectedClients = new List<TcpClient>();

                foreach (var client in clients)
                {
                    try
                    {
                        if (client.Connected)
                        {
                            NetworkStream stream = client.GetStream();
                            stream.Write(data, 0, data.Length);
                        }
                        else
                        {
                            disconnectedClients.Add(client);
                        }
                    }
                    catch (Exception)
                    {
                        disconnectedClients.Add(client);
                    }
                }

                // Remove disconnected clients
                foreach (var client in disconnectedClients)
                {
                    clients.Remove(client);
                    client.Dispose();
                }

                if (disconnectedClients.Count > 0)
                {
                    logger.LogInformation(
                        $"Removed {disconnectedClients.Count} disconnected clients. Remaining: {clients.Count}"
                    );
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Error publishing game state: {ex.Message}");
            }
        }

        private object ConvertGameStateForVisualization(
            GameState gameState,
            Dictionary<string, int> scores
        )
        {
            // Extract walls
            List<int[]> walls = new List<int[]>();
            for (int y = 0; y < gameState.Map.Height; y++)
            {
                for (int x = 0; x < gameState.Map.Width; x++)
                {
                    if (gameState.Map.Cells[y, x].Content == CellContent.Wall)
                    {
                        walls.Add(new int[] { x, y });
                    }
                }
            }

            // Extract pellets
            List<int[]> pellets = new List<int[]>();
            for (int y = 0; y < gameState.Map.Height; y++)
            {
                for (int x = 0; x < gameState.Map.Width; x++)
                {
                    if (gameState.Map.Cells[y, x].Content == CellContent.Pellet)
                    {
                        pellets.Add(new int[] { x, y });
                    }
                }
            }

            // Extract zookeeper positions
            List<int[]> zookeepers = new List<int[]>();
            foreach (var zookeeper in gameState.Zookeepers)
            {
                zookeepers.Add(new int[] { zookeeper.Position.X, zookeeper.Position.Y });
            }

            // Extract bot positions
            int[] rlBotPos = null;
            int[] refBotPos = null;

            foreach (var animal in gameState.Animals)
            {
                if (animal.BotId == "RLBot")
                {
                    rlBotPos = new int[] { animal.Position.X, animal.Position.Y };
                }
                else if (animal.BotId == "RefBot")
                {
                    refBotPos = new int[] { animal.Position.X, animal.Position.Y };
                }
            }

            // Create visualization data object
            var visualizationData = new
            {
                game_state = new
                {
                    walls = walls,
                    pellets = pellets,
                    zookeeper = zookeepers.Count > 0 ? zookeepers[0] : new int[] { 0, 0 },
                    rl_bot = rlBotPos ?? new int[] { 0, 0 },
                    ref_bot = refBotPos ?? new int[] { 0, 0 },
                },
                scores = new
                {
                    tick = gameState.Tick,
                    rl_score = scores.ContainsKey("RLBot") ? scores["RLBot"] : 0,
                    ref_score = scores.ContainsKey("RefBot") ? scores["RefBot"] : 0,
                    rl_captures = 0, // These would need to be tracked separately
                    ref_captures = 0,
                },
            };

            return visualizationData;
        }

        public void Stop()
        {
            isRunning = false;

            foreach (var client in clients)
            {
                try
                {
                    client.Close();
                }
                catch { }
            }

            clients.Clear();

            try
            {
                server?.Stop();
            }
            catch { }

            logger.LogInformation("Game state publisher stopped");
        }
    }

    public class RLBotProgram
    {
        private static GameStatePublisher publisher;

        public static void Main(string[] args)
        {
            // Setup logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });
            var logger = loggerFactory.CreateLogger<GameStatePublisher>();

            // Create and start the game state publisher
            publisher = new GameStatePublisher(logger);
            publisher.Start();

            // Create score tracking dictionary
            var scores = new Dictionary<string, int> { { "RLBot", 0 }, { "RefBot", 0 } };

            // Create the RL bot service
            var rlBotService = new RLBotService();

            // Main game loop would go here
            // For each game tick:
            // 1. Get game state
            // 2. Update scores
            // 3. Publish game state and scores
            // 4. Make bot decisions

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();

            // Stop the publisher when done
            publisher.Stop();
        }
    }

    public class RLBotService
    {
        // This would be the actual RL bot implementation
        // It would use the trained model to make decisions
    }
}
