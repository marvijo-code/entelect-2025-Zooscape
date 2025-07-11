﻿using System;
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

    public enum MoveDirection
    {
        Up,
        Down,
        Left,
        Right
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
        
        // Enhanced pellet analysis - unlimited counting
        public int TotalLinkedPelletsUp { get; set; }
        public int TotalLinkedPelletsLeft { get; set; }
        public int TotalLinkedPelletsRight { get; set; }
        public int TotalLinkedPelletsDown { get; set; }
        public int TotalPelletsInLineOfSight { get; set; }
        public int LargestPelletClusterSize { get; set; }
        public int PelletClustersCount { get; set; }
        
        public int[] PelletsPerQuadrant { get; set; } = new int[4];
        public int CurrentQuadrant { get; set; } = -1;
        public int NearestZookeeperDist { get; set; } = int.MaxValue;
        public (int x, int y) NearestZookeeperPos { get; set; } = (-1, -1);
        
        // Move analysis fields
        public bool IsAnalyzingMove { get; set; }
        public MoveDirection? AnalyzedMove { get; set; }
        public (int x, int y) OriginalPos { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: GameStateInspector <json-file-path> [bot-nickname] [--analyze-move <direction>]");
                Console.WriteLine("Example: GameStateInspector gamestate.json MyBot");
                Console.WriteLine("Example: GameStateInspector gamestate.json MyBot --analyze-move Up");
                Console.WriteLine("Directions: Up, Down, Left, Right");
                Console.WriteLine("If no bot nickname is provided, all animals will be listed.");
                return;
            }

            string filePath = args[0];
            string? botNickname = args.Length > 1 ? args[1] : null;
            MoveDirection? moveToAnalyze = null;

            // Parse --analyze-move argument
            for (int i = 2; i < args.Length; i++)
            {
                if (args[i] == "--analyze-move" && i + 1 < args.Length)
                {
                    if (Enum.TryParse<MoveDirection>(args[i + 1], true, out var direction))
                    {
                        moveToAnalyze = direction;
                    }
                    else
                    {
                        Console.WriteLine($"Invalid move direction: {args[i + 1]}. Valid directions: Up, Down, Left, Right");
                        return;
                    }
                    break;
                }
            }

            if (botNickname == null)
            {
                ListAllAnimals(filePath);
                return;
            }

            var analysis = AnalyzeStateFromFile(filePath, botNickname, moveToAnalyze);
            if (analysis != null)
            {
                PrintAnalysis(analysis);
            }
        }

        static void ListAllAnimals(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"File not found: {filePath}");
                return;
            }

            var json = File.ReadAllText(filePath);
            var gameState = JsonSerializer.Deserialize<GameState>(json);

            if (gameState == null)
            {
                Console.WriteLine("Failed to deserialize game state");
                return;
            }

            Console.WriteLine("Animals in game state:");
            Console.WriteLine("=====================");
            for (int i = 0; i < gameState.Animals.Length; i++)
            {
                var animal = gameState.Animals[i];
                Console.WriteLine($"{i + 1}. Nickname: '{animal.Nickname}' | ID: {animal.Id} | Position: ({animal.X}, {animal.Y}) | Score: {animal.Score}");
            }
            Console.WriteLine();
            Console.WriteLine("To analyze a specific animal, run:");
            Console.WriteLine($"GameStateInspector \"{filePath}\" \"<nickname>\"");
            Console.WriteLine();
            Console.WriteLine("To analyze what happens if an animal makes a move, run:");
            Console.WriteLine($"GameStateInspector \"{filePath}\" \"<nickname>\" --analyze-move <direction>");
            Console.WriteLine("Example: GameStateInspector \"gamestate.json\" \"MyBot\" --analyze-move Up");
        }

        static StateAnalysis? AnalyzeStateFromFile(string filePath, string botNickname, MoveDirection? moveToAnalyze = null)
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

            return AnalyzeState(gameState, botNickname, moveToAnalyze);
        }

        static StateAnalysis? AnalyzeState(GameState gameState, string botNickname, MoveDirection? moveToAnalyze = null)
        {
            var me = gameState.Animals.FirstOrDefault(a => a.Nickname == botNickname);
            if (me == null)
            {
                Console.WriteLine($"Bot with nickname '{botNickname}' not found in game state");
                return null;
            }

            var originalPos = (me.X, me.Y);
            var analysisPos = originalPos;

            // If analyzing a move, calculate the new position
            if (moveToAnalyze.HasValue)
            {
                analysisPos = ApplyMove(originalPos, moveToAnalyze.Value);
                
                // Check if the move is valid (not into a wall)
                var tempCellLookup = gameState.Cells.ToDictionary(c => (c.X, c.Y), c => c.Content);
                if (tempCellLookup.TryGetValue(analysisPos, out var content) && content == CellContent.Wall)
                {
                    Console.WriteLine($"Invalid move: {moveToAnalyze} would move into a wall at ({analysisPos.Item1}, {analysisPos.Item2})");
                    return null;
                }
                
                if (!tempCellLookup.ContainsKey(analysisPos))
                {
                    Console.WriteLine($"Invalid move: {moveToAnalyze} would move out of bounds to ({analysisPos.Item1}, {analysisPos.Item2})");
                    return null;
                }
            }

            var analysis = new StateAnalysis
            {
                MyPos = analysisPos,
                Score = me.Score,
                IsAnalyzingMove = moveToAnalyze.HasValue,
                AnalyzedMove = moveToAnalyze,
                OriginalPos = originalPos
            };

            // Create a lookup for cells by position
            var cellLookup = gameState.Cells.ToDictionary(c => (c.X, c.Y), c => c.Content);

            // Check for pellets in each direction (limited to 3 steps)
            bool pelletUp, pelletLeft, pelletRight, pelletDown;
            int pelletsUpTo3, pelletsLeftTo3, pelletsRightTo3, pelletsDownTo3;
            
            CheckPelletDirection(cellLookup, analysis.MyPos, 0, -1, out pelletUp, out pelletsUpTo3);
            CheckPelletDirection(cellLookup, analysis.MyPos, -1, 0, out pelletLeft, out pelletsLeftTo3);
            CheckPelletDirection(cellLookup, analysis.MyPos, 1, 0, out pelletRight, out pelletsRightTo3);
            CheckPelletDirection(cellLookup, analysis.MyPos, 0, 1, out pelletDown, out pelletsDownTo3);
            
            analysis.PelletUp = pelletUp;
            analysis.PelletLeft = pelletLeft;
            analysis.PelletRight = pelletRight;
            analysis.PelletDown = pelletDown;
            analysis.PelletsUpTo3 = pelletsUpTo3;
            analysis.PelletsLeftTo3 = pelletsLeftTo3;
            analysis.PelletsRightTo3 = pelletsRightTo3;
            analysis.PelletsDownTo3 = pelletsDownTo3;

            // Count consecutive pellets (unlimited)
            analysis.ConsecutivePelletsUp = CountConsecutivePellets(cellLookup, analysis.MyPos, 0, -1);
            analysis.ConsecutivePelletsLeft = CountConsecutivePellets(cellLookup, analysis.MyPos, -1, 0);
            analysis.ConsecutivePelletsRight = CountConsecutivePellets(cellLookup, analysis.MyPos, 1, 0);
            analysis.ConsecutivePelletsDown = CountConsecutivePellets(cellLookup, analysis.MyPos, 0, 1);

            // Calculate linked pellets in each direction (connected pellets up to depth 10 or count 50)
            analysis.TotalLinkedPelletsUp = CountLinkedPelletsInDirection(cellLookup, analysis.MyPos, 0, -1);
            analysis.TotalLinkedPelletsLeft = CountLinkedPelletsInDirection(cellLookup, analysis.MyPos, -1, 0);
            analysis.TotalLinkedPelletsRight = CountLinkedPelletsInDirection(cellLookup, analysis.MyPos, 1, 0);
            analysis.TotalLinkedPelletsDown = CountLinkedPelletsInDirection(cellLookup, analysis.MyPos, 0, 1);
            
            // Calculate total pellets in line of sight (all directions)
            analysis.TotalPelletsInLineOfSight = analysis.TotalLinkedPelletsUp + 
                                                analysis.TotalLinkedPelletsLeft + 
                                                analysis.TotalLinkedPelletsRight + 
                                                analysis.TotalLinkedPelletsDown;

            // Analyze pellet clusters
            AnalyzePelletClusters(cellLookup, analysis);

            // Calculate quadrants
            CalculateQuadrants(gameState, analysis);

            // Find nearest zookeeper
            FindNearestZookeeper(gameState, analysis);

            return analysis;
        }

        static (int x, int y) ApplyMove((int x, int y) pos, MoveDirection move)
        {
            return move switch
            {
                MoveDirection.Up => (pos.x, pos.y - 1),
                MoveDirection.Down => (pos.x, pos.y + 1),
                MoveDirection.Left => (pos.x - 1, pos.y),
                MoveDirection.Right => (pos.x + 1, pos.y),
                _ => pos
            };
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

        static int CountLinkedPelletsInDirection(Dictionary<(int, int), CellContent> cellLookup, (int x, int y) pos, int dx, int dy)
        {
            const int MAX_DEPTH = 10;
            const int MAX_COUNT = 50;
            
            var visited = new HashSet<(int, int)>();
            var queue = new Queue<((int x, int y) pos, int depth)>();
            int count = 0;
            
            // Start exploring from the first position in the given direction
            var startPos = (pos.x + dx, pos.y + dy);
            if (cellLookup.TryGetValue(startPos, out var content) && content == CellContent.Pellet)
            {
                queue.Enqueue((startPos, 1));
                visited.Add(startPos);
            }
            
            // BFS to find all connected pellets in the general direction
            while (queue.Count > 0 && count < MAX_COUNT)
            {
                var (currentPos, depth) = queue.Dequeue();
                count++;
                
                if (depth >= MAX_DEPTH) continue;
                
                // Check all 4 directions from current pellet, but prioritize the main direction
                var directions = new[] { (dx, dy), (0, 1), (0, -1), (1, 0), (-1, 0) };
                
                foreach (var (ddx, ddy) in directions)
                {
                    var nextPos = (currentPos.x + ddx, currentPos.y + ddy);
                    
                    // Skip if already visited
                    if (visited.Contains(nextPos)) continue;
                    
                    // Check if it's a pellet and in the general direction we're exploring
                    if (cellLookup.TryGetValue(nextPos, out var nextContent) && nextContent == CellContent.Pellet)
                    {
                        // Check if this pellet is in the same general direction from the original position
                        if (IsInGeneralDirection(pos, nextPos, dx, dy))
                        {
                            queue.Enqueue((nextPos, depth + 1));
                            visited.Add(nextPos);
                        }
                    }
                }
            }
            
            return count;
        }
        
        static bool IsInGeneralDirection((int x, int y) origin, (int x, int y) target, int dx, int dy)
        {
            int deltaX = target.x - origin.x;
            int deltaY = target.y - origin.y;
            
            // For horizontal directions (left/right)
            if (dx != 0)
            {
                return (dx > 0 && deltaX > 0) || (dx < 0 && deltaX < 0);
            }
            
            // For vertical directions (up/down)
            if (dy != 0)
            {
                return (dy > 0 && deltaY > 0) || (dy < 0 && deltaY < 0);
            }
            
            return false;
        }

        static void AnalyzePelletClusters(Dictionary<(int, int), CellContent> cellLookup, StateAnalysis analysis)
        {
            var pelletPositions = cellLookup.Where(kvp => kvp.Value == CellContent.Pellet)
                                           .Select(kvp => kvp.Key)
                                           .ToHashSet();
            
            var visited = new HashSet<(int, int)>();
            var clusterSizes = new List<int>();

            foreach (var pelletPos in pelletPositions)
            {
                if (!visited.Contains(pelletPos))
                {
                    int clusterSize = ExploreCluster(pelletPositions, visited, pelletPos);
                    if (clusterSize > 0)
                        clusterSizes.Add(clusterSize);
                }
            }

            analysis.PelletClustersCount = clusterSizes.Count;
            analysis.LargestPelletClusterSize = clusterSizes.Any() ? clusterSizes.Max() : 0;
        }

        static int ExploreCluster(HashSet<(int, int)> pelletPositions, HashSet<(int, int)> visited, (int x, int y) start)
        {
            var stack = new Stack<(int, int)>();
            stack.Push(start);
            int clusterSize = 0;

            var directions = new[] { (0, 1), (0, -1), (1, 0), (-1, 0) }; // Up, Down, Right, Left

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                
                if (visited.Contains(current) || !pelletPositions.Contains(current))
                    continue;

                visited.Add(current);
                clusterSize++;

                // Check all adjacent positions
                foreach (var (dx, dy) in directions)
                {
                    var adjacent = (current.Item1 + dx, current.Item2 + dy);
                    if (pelletPositions.Contains(adjacent) && !visited.Contains(adjacent))
                    {
                        stack.Push(adjacent);
                    }
                }
            }

            return clusterSize;
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
            Console.WriteLine("=== GAME STATE ANALYSIS ===");
            if (analysis.IsAnalyzingMove)
            {
                Console.WriteLine($"Original Bot Position: ({analysis.OriginalPos.x}, {analysis.OriginalPos.y})");
                Console.WriteLine($"After Move {analysis.AnalyzedMove}: ({analysis.MyPos.x}, {analysis.MyPos.y})");
                Console.WriteLine($"*** ANALYZING PELLETS FROM NEW POSITION ***");
            }
            else
            {
                Console.WriteLine($"Bot Position: ({analysis.MyPos.x}, {analysis.MyPos.y})");
            }
            Console.WriteLine($"Score: {analysis.Score}");
            Console.WriteLine();

            string yesNo(bool value) => value ? "Yes" : "No";
            
            Console.WriteLine("=== IMMEDIATE PELLET AVAILABILITY ===");
            Console.WriteLine($"Pellet Up? {yesNo(analysis.PelletUp)}");
            Console.WriteLine($"Pellet Left? {yesNo(analysis.PelletLeft)}");
            Console.WriteLine($"Pellet Right? {yesNo(analysis.PelletRight)}");
            Console.WriteLine($"Pellet Down? {yesNo(analysis.PelletDown)}");
            Console.WriteLine();

            Console.WriteLine("=== PELLETS IN 3-STEP RANGE ===");
            Console.WriteLine($"Pellets Up in 3 steps: {analysis.PelletsUpTo3}");
            Console.WriteLine($"Pellets Left in 3 steps: {analysis.PelletsLeftTo3}");
            Console.WriteLine($"Pellets Right in 3 steps: {analysis.PelletsRightTo3}");
            Console.WriteLine($"Pellets Down in 3 steps: {analysis.PelletsDownTo3}");
            Console.WriteLine();

            Console.WriteLine("=== CONSECUTIVE PELLETS (LINE OF SIGHT) ===");
            Console.WriteLine($"Consecutive Pellets Up: {analysis.ConsecutivePelletsUp}");
            Console.WriteLine($"Consecutive Pellets Left: {analysis.ConsecutivePelletsLeft}");
            Console.WriteLine($"Consecutive Pellets Right: {analysis.ConsecutivePelletsRight}");
            Console.WriteLine($"Consecutive Pellets Down: {analysis.ConsecutivePelletsDown}");
            Console.WriteLine();

            Console.WriteLine("=== TOTAL LINKED PELLETS (UNLIMITED) ===");
            Console.WriteLine($"Total Linked Pellets Up: {analysis.TotalLinkedPelletsUp}");
            Console.WriteLine($"Total Linked Pellets Left: {analysis.TotalLinkedPelletsLeft}");
            Console.WriteLine($"Total Linked Pellets Right: {analysis.TotalLinkedPelletsRight}");
            Console.WriteLine($"Total Linked Pellets Down: {analysis.TotalLinkedPelletsDown}");
            Console.WriteLine($"Total Pellets in Line of Sight: {analysis.TotalPelletsInLineOfSight}");
            Console.WriteLine();

            Console.WriteLine("=== PELLET CLUSTER ANALYSIS ===");
            Console.WriteLine($"Number of Pellet Clusters: {analysis.PelletClustersCount}");
            Console.WriteLine($"Largest Pellet Cluster Size: {analysis.LargestPelletClusterSize}");
            Console.WriteLine();

            Console.WriteLine("=== QUADRANT ANALYSIS ===");
            string[] quadNames = { "Top-Right", "Top-Left", "Bottom-Left", "Bottom-Right" };
            for (int q = 0; q < 4; q++)
            {
                Console.WriteLine($"Pellets in {quadNames[q]}: {analysis.PelletsPerQuadrant[q]}");
            }
            Console.WriteLine($"Current Quadrant: {(analysis.CurrentQuadrant >= 0 ? quadNames[analysis.CurrentQuadrant] : "Unknown")}");
            Console.WriteLine();

            Console.WriteLine("=== ZOOKEEPER ANALYSIS ===");
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
