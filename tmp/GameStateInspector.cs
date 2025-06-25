using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameStateInspector
{
    public enum CellContent
    {
        Empty = 0,
        Wall = 1,
        Pellet = 2,
        PowerUp = 3,
        EscapeZone = 4
    }

    public class Cell
    {
        [JsonPropertyName("X")]
        public int X { get; set; }
        
        [JsonPropertyName("Y")]
        public int Y { get; set; }
        
        [JsonPropertyName("Content")]
        public CellContent Content { get; set; }
    }

    public class Animal
    {
        [JsonPropertyName("Id")]
        public string Id { get; set; } = "";
        
        [JsonPropertyName("Nickname")]
        public string Nickname { get; set; } = "";
        
        [JsonPropertyName("X")]
        public int X { get; set; }
        
        [JsonPropertyName("Y")]
        public int Y { get; set; }
        
        [JsonPropertyName("Score")]
        public int Score { get; set; }
    }

    public class Zookeeper
    {
        [JsonPropertyName("Id")]
        public string Id { get; set; } = "";
        
        [JsonPropertyName("Nickname")]
        public string Nickname { get; set; } = "";
        
        [JsonPropertyName("X")]
        public int X { get; set; }
        
        [JsonPropertyName("Y")]
        public int Y { get; set; }
    }

    public class GameState
    {
        [JsonPropertyName("Cells")]
        public Cell[] Cells { get; set; } = Array.Empty<Cell>();
        
        [JsonPropertyName("Animals")]
        public Animal[] Animals { get; set; } = Array.Empty<Animal>();
        
        [JsonPropertyName("Zookeepers")]
        public Zookeeper[] Zookeepers { get; set; } = Array.Empty<Zookeeper>();
    }

    public class StateAnalysis
    {
        public (int x, int y) MyPos { get; set; }
        public int Score { get; set; }
        public bool PelletUp { get; set; }
        public bool PelletLeft { get; set; }
        public bool PelletRight { get; set; }
        public bool PelletDown { get; set; }
        public int PelletsUpTo3 { get; set; }
        public int PelletsLeftTo3 { get; set; }
        public int PelletsRightTo3 { get; set; }
        public int PelletsDownTo3 { get; set; }
        public int ConsecutivePelletsUp { get; set; }
        public int ConsecutivePelletsLeft { get; set; }
        public int ConsecutivePelletsRight { get; set; }
        public int ConsecutivePelletsDown { get; set; }
        public int[] PelletsPerQuadrant { get; set; } = new int[4];
        public int CurrentQuadrant { get; set; } = -1;
        public int NearestZookeeperDist { get; set; } = int.MaxValue;
        public (int x, int y) NearestZookeeperPos { get; set; } = (-1, -1);
    }

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: GameStateInspector <jsonPath> <botNickname>");
                return;
            }

            string jsonPath = args[0];
            string botNickname = args[1];

            try
            {
                var analysis = AnalyzeStateFromFile(jsonPath, botNickname);
                if (analysis == null)
                {
                    Console.WriteLine($"Failed to analyze state from file: {jsonPath}");
                    return;
                }

                PrintAnalysis(analysis);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        static StateAnalysis? AnalyzeStateFromFile(string filePath, string botNickname)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"File not found: {filePath}");
                return null;
            }

            var json = File.ReadAllText(filePath);
            var gameState = JsonSerializer.Deserialize<GameState>(json);

            if (gameState == null)
            {
                Console.WriteLine("Failed to deserialize game state");
                return null;
            }

            return AnalyzeState(gameState, botNickname);
        }

        static StateAnalysis? AnalyzeState(GameState gameState, string botNickname)
        {
            var me = gameState.Animals.FirstOrDefault(a => a.Nickname == botNickname);
            if (me == null)
            {
                Console.WriteLine($"Bot with nickname '{botNickname}' not found in game state");
                return null;
            }

            var analysis = new StateAnalysis
            {
                MyPos = (me.X, me.Y),
                Score = me.Score
            };

            // Create a lookup for cells by position
            var cellLookup = gameState.Cells.ToDictionary(c => (c.X, c.Y), c => c.Content);

            // Check for pellets in each direction
            CheckPelletDirection(cellLookup, analysis.MyPos, 0, -1, out analysis.PelletUp, out analysis.PelletsUpTo3);
            CheckPelletDirection(cellLookup, analysis.MyPos, -1, 0, out analysis.PelletLeft, out analysis.PelletsLeftTo3);
            CheckPelletDirection(cellLookup, analysis.MyPos, 1, 0, out analysis.PelletRight, out analysis.PelletsRightTo3);
            CheckPelletDirection(cellLookup, analysis.MyPos, 0, 1, out analysis.PelletDown, out analysis.PelletsDownTo3);

            // Count consecutive pellets
            analysis.ConsecutivePelletsUp = CountConsecutivePellets(cellLookup, analysis.MyPos, 0, -1);
            analysis.ConsecutivePelletsLeft = CountConsecutivePellets(cellLookup, analysis.MyPos, -1, 0);
            analysis.ConsecutivePelletsRight = CountConsecutivePellets(cellLookup, analysis.MyPos, 1, 0);
            analysis.ConsecutivePelletsDown = CountConsecutivePellets(cellLookup, analysis.MyPos, 0, 1);

            // Calculate quadrants
            CalculateQuadrants(gameState, analysis);

            // Find nearest zookeeper
            FindNearestZookeeper(gameState, analysis);

            return analysis;
        }

        static void CheckPelletDirection(Dictionary<(int, int), CellContent> cellLookup, (int x, int y) pos, int dx, int dy, out bool pelletAdjacent, out int pelletsInThree)
        {
            pelletAdjacent = false;
            pelletsInThree = 0;

            for (int step = 1; step <= 3; step++)
            {
                var checkPos = (pos.x + dx * step, pos.y + dy * step);
                if (cellLookup.TryGetValue(checkPos, out var content) && content == CellContent.Pellet)
                {
                    if (step == 1)
                        pelletAdjacent = true;
                    pelletsInThree++;
                }
            }
        }

        static int CountConsecutivePellets(Dictionary<(int, int), CellContent> cellLookup, (int x, int y) pos, int dx, int dy)
        {
            int count = 0;
            var current = (pos.x + dx, pos.y + dy);

            while (cellLookup.TryGetValue(current, out var content) && content == CellContent.Pellet)
            {
                count++;
                current = (current.Item1 + dx, current.Item2 + dy);
            }

            return count;
        }

        static void CalculateQuadrants(GameState gameState, StateAnalysis analysis)
        {
            var allCells = gameState.Cells;
            if (!allCells.Any()) return;

            int minX = allCells.Min(c => c.X);
            int maxX = allCells.Max(c => c.X);
            int minY = allCells.Min(c => c.Y);
            int maxY = allCells.Max(c => c.Y);

            int midX = (minX + maxX) / 2;
            int midY = (minY + maxY) / 2;

            // Count pellets in each quadrant
            foreach (var cell in allCells.Where(c => c.Content == CellContent.Pellet))
            {
                int quadrant = GetQuadrant(cell.X, cell.Y, midX, midY);
                if (quadrant >= 0 && quadrant < 4)
                    analysis.PelletsPerQuadrant[quadrant]++;
            }

            // Determine current quadrant
            analysis.CurrentQuadrant = GetQuadrant(analysis.MyPos.x, analysis.MyPos.y, midX, midY);
        }

        static int GetQuadrant(int x, int y, int midX, int midY)
        {
            if (x > midX && y <= midY) return 0; // Top-Right
            if (x <= midX && y <= midY) return 1; // Top-Left
            if (x <= midX && y > midY) return 2; // Bottom-Left
            if (x > midX && y > midY) return 3; // Bottom-Right
            return -1;
        }

        static void FindNearestZookeeper(GameState gameState, StateAnalysis analysis)
        {
            if (!gameState.Zookeepers.Any()) return;

            int minDist = int.MaxValue;
            (int x, int y) nearestPos = (-1, -1);

            foreach (var zk in gameState.Zookeepers)
            {
                int dist = Math.Abs(analysis.MyPos.x - zk.X) + Math.Abs(analysis.MyPos.y - zk.Y);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestPos = (zk.X, zk.Y);
                }
            }

            analysis.NearestZookeeperDist = minDist;
            analysis.NearestZookeeperPos = nearestPos;
        }

        static void PrintAnalysis(StateAnalysis analysis)
        {
            Console.WriteLine($"Bot Position: ({analysis.MyPos.x}, {analysis.MyPos.y})");
            Console.WriteLine($"Score: {analysis.Score}");

            string yesNo(bool value) => value ? "Yes" : "No";
            Console.WriteLine($"Pellet Up? {yesNo(analysis.PelletUp)}");
            Console.WriteLine($"Pellet Left? {yesNo(analysis.PelletLeft)}");
            Console.WriteLine($"Pellet Right? {yesNo(analysis.PelletRight)}");
            Console.WriteLine($"Pellet Down? {yesNo(analysis.PelletDown)}");

            Console.WriteLine($"Pellets Up in 3 steps: {analysis.PelletsUpTo3}");
            Console.WriteLine($"Pellets Left in 3 steps: {analysis.PelletsLeftTo3}");
            Console.WriteLine($"Pellets Right in 3 steps: {analysis.PelletsRightTo3}");
            Console.WriteLine($"Pellets Down in 3 steps: {analysis.PelletsDownTo3}");

            Console.WriteLine($"Consecutive Pellets Up: {analysis.ConsecutivePelletsUp}");
            Console.WriteLine($"Consecutive Pellets Left: {analysis.ConsecutivePelletsLeft}");
            Console.WriteLine($"Consecutive Pellets Right: {analysis.ConsecutivePelletsRight}");
            Console.WriteLine($"Consecutive Pellets Down: {analysis.ConsecutivePelletsDown}");

            string[] quadNames = { "Top-Right", "Top-Left", "Bottom-Left", "Bottom-Right" };
            for (int q = 0; q < 4; q++)
            {
                Console.WriteLine($"Pellets in {quadNames[q]}: {analysis.PelletsPerQuadrant[q]}");
            }
            Console.WriteLine($"Current Quadrant: {(analysis.CurrentQuadrant >= 0 ? quadNames[analysis.CurrentQuadrant] : "Unknown")}");

            if (analysis.NearestZookeeperDist != int.MaxValue)
            {
                Console.WriteLine($"Nearest Zookeeper: ({analysis.NearestZookeeperPos.x}, {analysis.NearestZookeeperPos.y}) at distance {analysis.NearestZookeeperDist}");
            }
            else
            {
                Console.WriteLine("No zookeepers present.");
            }
        }
    }
} 