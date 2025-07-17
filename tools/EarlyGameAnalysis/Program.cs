using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

namespace EarlyGameAnalysis
{
    public class GameState
    {
        public DateTime TimeStamp { get; set; }
        public int Tick { get; set; }
        public List<Animal> Animals { get; set; } = new();
    }

    public class Animal
    {
        public Guid Id { get; set; }
        public string Nickname { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public int Score { get; set; }
        public bool IsViable { get; set; }
        public int CapturedCounter { get; set; }
        public int DistanceCovered { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: dotnet run -- <log-directory> <bot-nickname>");
                Console.WriteLine("Example: dotnet run -- \"../../logs/20250717_221852\" \"StaticHeuro\"");
                return;
            }

            string logDirectory = args[0];
            string botNickname = args[1];

            if (!Directory.Exists(logDirectory))
            {
                Console.WriteLine($"Log directory not found: {logDirectory}");
                return;
            }

            AnalyzeEarlyGamePointAccumulation(logDirectory, botNickname);
        }

        static void AnalyzeEarlyGamePointAccumulation(string logDirectory, string botNickname)
        {
            Console.WriteLine($"Analyzing early game point accumulation for bot: {botNickname}");
            Console.WriteLine($"Log directory: {logDirectory}");
            Console.WriteLine();

            var gameStates = new List<(int tick, GameState state)>();
            
            // Load first 20 ticks (early game)
            for (int tick = 1; tick <= 20; tick++)
            {
                string filePath = Path.Combine(logDirectory, $"{tick}.json");
                if (File.Exists(filePath))
                {
                    try
                    {
                        string json = File.ReadAllText(filePath);
                        var gameState = JsonSerializer.Deserialize<GameState>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        
                        if (gameState != null)
                        {
                            gameStates.Add((tick, gameState));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading tick {tick}: {ex.Message}");
                    }
                }
            }

            if (gameStates.Count == 0)
            {
                Console.WriteLine("No game states found in the specified directory.");
                return;
            }

            // Find the bot in the game states
            Animal? bot = null;
            foreach (var (tick, state) in gameStates)
            {
                bot = state.Animals.FirstOrDefault(a => a.Nickname.Equals(botNickname, StringComparison.OrdinalIgnoreCase));
                if (bot != null) break;
            }

            if (bot == null)
            {
                Console.WriteLine($"Bot '{botNickname}' not found in any game state.");
                Console.WriteLine("Available bots:");
                foreach (var (tick, state) in gameStates.Take(1))
                {
                    foreach (var animal in state.Animals)
                    {
                        Console.WriteLine($"  - {animal.Nickname} (ID: {animal.Id})");
                    }
                }
                return;
            }

            Console.WriteLine($"Found bot: {bot.Nickname} (ID: {bot.Id})");
            Console.WriteLine();

            // Analyze point accumulation
            var botId = bot.Id;
            var scoreProgression = new List<(int tick, int score, int scoreDelta, bool isViable)>();
            int previousScore = 0;

            Console.WriteLine("Early Game Score Progression:");
            Console.WriteLine("Tick | Score | Delta | Viable | Status");
            Console.WriteLine("-----|-------|-------|--------|-------");

            foreach (var (tick, state) in gameStates.OrderBy(x => x.tick))
            {
                var currentBot = state.Animals.FirstOrDefault(a => a.Id == botId);
                if (currentBot != null)
                {
                    int currentScore = currentBot.Score;
                    int scoreDelta = currentScore - previousScore;
                    bool isViable = currentBot.IsViable;
                    
                    scoreProgression.Add((tick, currentScore, scoreDelta, isViable));
                    
                    string status = !isViable ? "NOT_VIABLE" : 
                                   scoreDelta == 0 ? "NO_POINTS" : 
                                   scoreDelta > 0 ? "GAINED" : "LOST";
                    
                    string viableStr = isViable ? "Yes" : "No";
                    Console.WriteLine($"{tick,4} | {currentScore,5} | {scoreDelta,5} | {viableStr,6} | {status}");
                    
                    if (isViable) // Only update previous score when bot is viable
                        previousScore = currentScore;
                }
            }

            Console.WriteLine();

            // Identify problematic ticks (viable bot but no points gained)
            var viableTicks = scoreProgression.Where(x => x.isViable).ToList();
            var noPointTicks = viableTicks.Where(x => x.scoreDelta == 0 && x.tick > 1).ToList();
            
            Console.WriteLine();
            Console.WriteLine("Analysis Results:");
            Console.WriteLine($"  Total ticks analyzed: {scoreProgression.Count}");
            Console.WriteLine($"  Ticks where bot was viable: {viableTicks.Count}");
            Console.WriteLine($"  Viable ticks with no point gain: {noPointTicks.Count}");
            
            if (noPointTicks.Any())
            {
                Console.WriteLine();
                Console.WriteLine($"Found {noPointTicks.Count} viable ticks where bot didn't accumulate points:");
                foreach (var (tick, score, _, _) in noPointTicks)
                {
                    Console.WriteLine($"  - Tick {tick}: Score remained at {score} (bot was viable but gained no points)");
                }
                Console.WriteLine();
                
                // Create test cases for the first few problematic ticks
                Console.WriteLine("Suggested test cases to create:");
                foreach (var (tick, score, _, _) in noPointTicks.Take(3))
                {
                    string gameStateFile = Path.Combine(logDirectory, $"{tick}.json");
                    Console.WriteLine($"  - Tick {tick}: .\\create_test.ps1 -GameStateFile \"{gameStateFile}\" -BotNickname \"{botNickname}\" -TestName \"EarlyGame_NoPoints_Tick{tick}\"");
                }
            }
            else if (viableTicks.Any())
            {
                Console.WriteLine();
                Console.WriteLine("No issues found - bot accumulated points consistently when viable in early game.");
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("Bot was not viable in any of the analyzed early game ticks.");
            }

            // Summary statistics
            Console.WriteLine();
            Console.WriteLine("Summary:");
            Console.WriteLine($"  Total ticks analyzed: {scoreProgression.Count}");
            Console.WriteLine($"  Viable ticks with no point gain: {noPointTicks.Count}");
            
            if (scoreProgression.Any())
            {
                var lastTick = scoreProgression.Last();
                Console.WriteLine($"  Final score at tick {lastTick.tick}: {lastTick.score}");
                
                if (viableTicks.Count > 1)
                {
                    double avgPointsPerViableTick = (double)lastTick.score / (viableTicks.Count - 1);
                    Console.WriteLine($"  Average points per viable tick: {avgPointsPerViableTick:F2}");
                }
            }
        }
    }
}
