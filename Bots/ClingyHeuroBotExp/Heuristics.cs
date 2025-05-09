using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using HeuroBot.Enums;
using HeuroBot.Models;

namespace HeuroBot.Services;

public static class Heuristics
{
    // Keep track of recently visited positions to avoid cycles
    private static readonly ConcurrentDictionary<string, Queue<(int, int)>> _recentPositions =
        new ConcurrentDictionary<string, Queue<(int, int)>>();

    // Track the last direction for each animal to detect reversals and turns
    private static readonly ConcurrentDictionary<string, BotAction> _lastDirections =
        new ConcurrentDictionary<string, BotAction>();

    // Reset counters if needed based on game state
    private static void UpdatePathMemory(GameState state, Animal me)
    {
        string animalKey = me.Id.ToString();

        // Initialize or get position history for this animal
        if (!_recentPositions.ContainsKey(animalKey))
        {
            _recentPositions[animalKey] = new Queue<(int, int)>();
        }

        // Add current position to history
        var positions = _recentPositions[animalKey];
        positions.Enqueue((me.X, me.Y));

        // Keep only the most recent 10 positions to detect cycles
        while (positions.Count > 10)
        {
            positions.Dequeue();
        }

        // Clear history if animal was just captured (at spawn point)
        if (me.X == me.SpawnX && me.Y == me.SpawnY)
        {
            positions.Clear();
            _lastDirections.TryRemove(animalKey, out _);
        }
    }

    public static decimal ScoreMove(GameState state, Animal me, BotAction move)
    {
        decimal score = 0m;

        score += HeuristicsImpl.DistanceToGoal(state, me, move) * WEIGHTS.DistanceToGoal;
        score += HeuristicsImpl.OpponentProximity(state, me, move) * WEIGHTS.OpponentProximity;
        score += HeuristicsImpl.ResourceClustering(state, me, move) * WEIGHTS.ResourceClustering;
        score += HeuristicsImpl.AreaControl(state, me, move) * WEIGHTS.AreaControl;
        score += HeuristicsImpl.Mobility(state, me, move) * WEIGHTS.Mobility;
        score += HeuristicsImpl.PathSafety(state, me, move) * WEIGHTS.PathSafety;

        score += HeuristicsImpl.ZookeeperPrediction(state, me, move) * WEIGHTS.ZookeeperPrediction;
        score += HeuristicsImpl.CaptureAvoidance(state, me, move) * WEIGHTS.CaptureAvoidance;
        score += HeuristicsImpl.SpawnProximity(state, me, move) * WEIGHTS.SpawnProximity;
        score += HeuristicsImpl.TimeToCapture(state, me, move) * WEIGHTS.TimeToCapture;
        score += HeuristicsImpl.EdgeSafety(state, me, move) * WEIGHTS.EdgeSafety;
        score += HeuristicsImpl.QuadrantAwareness(state, me, move) * WEIGHTS.QuadrantAwareness;
        score += HeuristicsImpl.TargetEvaluation(state, me, move) * WEIGHTS.TargetEvaluation;
        score +=
            HeuristicsImpl.TiebreakersAwareness(state, me, move) * WEIGHTS.TiebreakersAwareness;
        score += HeuristicsImpl.ZookeeperCooldown(state, me, move) * WEIGHTS.ZookeeperCooldown;
        score += HeuristicsImpl.PelletEfficiency(state, me, move) * WEIGHTS.PelletEfficiency;
        score += HeuristicsImpl.EscapeRoutes(state, me, move) * WEIGHTS.EscapeRoutes;
        score += HeuristicsImpl.AnimalCongestion(state, me, move) * WEIGHTS.AnimalCongestion;
        score +=
            HeuristicsImpl.CaptureRecoveryStrategy(state, me, move)
            * WEIGHTS.CaptureRecoveryStrategy;

        // New winning heuristics
        score +=
            HeuristicsImpl.FirstCommandAdvantage(state, me, move) * WEIGHTS.FirstCommandAdvantage;
        score +=
            HeuristicsImpl.TravelDistanceMaximizer(state, me, move)
            * WEIGHTS.TravelDistanceMaximizer;
        score += HeuristicsImpl.SpawnTimeMinimizer(state, me, move) * WEIGHTS.SpawnTimeMinimizer;
        score += HeuristicsImpl.TimerAwareness(state, me, move) * WEIGHTS.TimerAwareness;
        score +=
            HeuristicsImpl.PelletRatioAwareness(state, me, move) * WEIGHTS.PelletRatioAwareness;
        score +=
            HeuristicsImpl.CommandQueueOptimization(state, me, move)
            * WEIGHTS.CommandQueueOptimization;
        score +=
            HeuristicsImpl.AnticipateCompetition(state, me, move) * WEIGHTS.AnticipateCompetition;
        score += HeuristicsImpl.EndgameStrategy(state, me, move) * WEIGHTS.EndgameStrategy;
        score += HeuristicsImpl.PositionalDominance(state, me, move) * WEIGHTS.PositionalDominance;
        score += HeuristicsImpl.ScoreLossMinimizer(state, me, move) * WEIGHTS.ScoreLossMinimizer;

        // Update path memory
        UpdatePathMemory(state, me);

        // Add anti-cycling measures
        score += HeuristicsImpl.CycleDetection(state, me, move) * WEIGHTS.CycleDetection;
        score += HeuristicsImpl.DirectionalVariety(state, me, move) * WEIGHTS.DirectionalVariety;
        score += HeuristicsImpl.EmptyCellAvoidance(state, me, move) * WEIGHTS.EmptyCellAvoidance;
        score +=
            HeuristicsImpl.AnimalProximityWhenTargeted(state, me, move)
            * WEIGHTS.AnimalProximityWhenTargeted;

        return score;
    }

    static class HeuristicsImpl
    {
        private static (int x, int y) ApplyMove(int x, int y, BotAction m) =>
            m switch
            {
                BotAction.Up => (x, y - 1),
                BotAction.Down => (x, y + 1),
                BotAction.Left => (x - 1, y),
                BotAction.Right => (x + 1, y),
                _ => (x, y),
            };

        private static bool IsTraversable(GameState state, int x, int y)
        {
            var cell = state.Cells.FirstOrDefault(c => c.X == x && c.Y == y);
            return cell != null && cell.Content != CellContent.Wall;
        }

        private static int ManhattanDistance(int x1, int y1, int x2, int y2) =>
            Math.Abs(x1 - x2) + Math.Abs(y1 - y2);

        private static List<(int x, int y)> GetPotentialZookeeperNextPositions(GameState state)
        {
            var result = new List<(int x, int y)>();
            foreach (var zookeeper in state.Zookeepers)
            {
                Animal targetAnimal = null;
                int minDistance = int.MaxValue;

                foreach (var animal in state.Animals.Where(a => a.IsViable))
                {
                    int distance = ManhattanDistance(zookeeper.X, zookeeper.Y, animal.X, animal.Y);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        targetAnimal = animal;
                    }
                }

                if (targetAnimal != null)
                {
                    if (
                        zookeeper.X < targetAnimal.X
                        && IsTraversable(state, zookeeper.X + 1, zookeeper.Y)
                    )
                        result.Add((zookeeper.X + 1, zookeeper.Y));
                    if (
                        zookeeper.X > targetAnimal.X
                        && IsTraversable(state, zookeeper.X - 1, zookeeper.Y)
                    )
                        result.Add((zookeeper.X - 1, zookeeper.Y));
                    if (
                        zookeeper.Y < targetAnimal.Y
                        && IsTraversable(state, zookeeper.X, zookeeper.Y + 1)
                    )
                        result.Add((zookeeper.X, zookeeper.Y + 1));
                    if (
                        zookeeper.Y > targetAnimal.Y
                        && IsTraversable(state, zookeeper.X, zookeeper.Y - 1)
                    )
                        result.Add((zookeeper.X, zookeeper.Y - 1));
                }

                result.Add((zookeeper.X, zookeeper.Y));
            }
            return result;
        }

        public static decimal DistanceToGoal(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            var pellets = state.Cells.Where(c => c.Content == CellContent.Pellet);
            if (!pellets.Any())
                return 0m;

            // Get the closest pellet
            var closestPellet = pellets
                .OrderBy(c => ManhattanDistance(c.X, c.Y, nx, ny))
                .FirstOrDefault();
            if (closestPellet == null)
                return 0m;

            int currentDist = ManhattanDistance(me.X, me.Y, closestPellet.X, closestPellet.Y);
            int newDist = ManhattanDistance(nx, ny, closestPellet.X, closestPellet.Y);

            // Heavily reward moves that get us closer to the nearest pellet
            if (newDist < currentDist)
                return 2.0m;
            else if (newDist > currentDist)
                return -1.0m;

            // If we're not making progress toward the closest pellet, consider the overall pellet situation
            var minDist = pellets.Min(c => ManhattanDistance(c.X, c.Y, nx, ny));
            return -minDist * 0.5m;
        }

        public static decimal OpponentProximity(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);

            // If there are no zookeepers, don't apply this heuristic
            if (!state.Zookeepers.Any())
                return 0m;

            // For each zookeeper, find the distance to our new position
            var myDistances = new List<(int zookeeperIndex, int distance)>();
            for (int i = 0; i < state.Zookeepers.Count; i++)
            {
                var zookeeper = state.Zookeepers[i];
                int distance = ManhattanDistance(zookeeper.X, zookeeper.Y, nx, ny);
                myDistances.Add((i, distance));
            }

            // Calculate minimum distance to any zookeeper
            var minDist = myDistances.Min(d => d.distance);

            // Check for each zookeeper if another animal is closer than us
            decimal score = 0m;
            foreach (var (zookeeperIndex, myDistance) in myDistances)
            {
                var zookeeper = state.Zookeepers[zookeeperIndex];

                // Find the closest animal to this zookeeper
                int minAnimalDistance = int.MaxValue;
                bool amIClosest = true;

                foreach (var animal in state.Animals.Where(a => a.IsViable && a.Id != me.Id))
                {
                    int animalDistance = ManhattanDistance(
                        zookeeper.X,
                        zookeeper.Y,
                        animal.X,
                        animal.Y
                    );
                    minAnimalDistance = Math.Min(minAnimalDistance, animalDistance);

                    // If any animal is closer than our new position would be, we're not the closest
                    if (animalDistance < myDistance)
                    {
                        amIClosest = false;
                    }
                }

                // Heavily penalize moves that would make us the closest animal to a zookeeper
                if (amIClosest)
                {
                    score -= 3.0m;
                }
                // Reward moves that maintain a buffer between us and the closest animal
                else if (myDistance > minAnimalDistance + 1)
                {
                    score += 1.0m;
                }
            }

            // Still consider absolute distance to zookeepers as in the original method
            score += 1m / (minDist + 1);

            return score;
        }

        public static decimal ResourceClustering(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);

            // Count pellets in a larger radius (3 instead of 2) to be more aware of clusters
            int pelletCount = state.Cells.Count(c =>
                c.Content == CellContent.Pellet && ManhattanDistance(c.X, c.Y, nx, ny) <= 3
            );

            // Give immediate cells higher weight
            int immediatePellets = state.Cells.Count(c =>
                c.Content == CellContent.Pellet && ManhattanDistance(c.X, c.Y, nx, ny) <= 1
            );

            // Prioritize moves that put us directly on a pellet or next to one
            return pelletCount + (immediatePellets * 2.0m);
        }

        public static decimal AreaControl(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            decimal value = 0;
            foreach (var cell in state.Cells.Where(c => c.Content == CellContent.Pellet))
            {
                int dist = ManhattanDistance(cell.X, cell.Y, nx, ny);
                if (dist <= 5) // Increased radius to 5
                    value += 1.0m / (dist + 1); // Weight by inverse distance
            }
            return value;
        }

        public static decimal Mobility(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            return Enum.GetValues<BotAction>()
                .Cast<BotAction>()
                .Count(a =>
                {
                    var (x2, y2) = ApplyMove(nx, ny, a);
                    return IsTraversable(state, x2, y2);
                });
        }

        public static decimal PathSafety(GameState state, Animal me, BotAction m) =>
            Mobility(state, me, m) <= 1 ? -1m : 0m;

        public static decimal ZookeeperPrediction(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            var zookeeperNextPositions = GetPotentialZookeeperNextPositions(state);

            if (zookeeperNextPositions.Any(pos => pos.x == nx && pos.y == ny))
                return -3.0m;

            int minDist = zookeeperNextPositions.Min(pos =>
                ManhattanDistance(pos.x, pos.y, nx, ny)
            );
            if (minDist <= 2)
                return -1.5m / (minDist + 0.5m);

            return 0m;
        }

        public static decimal CaptureAvoidance(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);

            bool amITarget = true;
            foreach (var zookeeper in state.Zookeepers)
            {
                int myDistance = ManhattanDistance(zookeeper.X, zookeeper.Y, me.X, me.Y);

                foreach (var animal in state.Animals.Where(a => a.IsViable && a.Id != me.Id))
                {
                    int theirDistance = ManhattanDistance(
                        zookeeper.X,
                        zookeeper.Y,
                        animal.X,
                        animal.Y
                    );
                    if (theirDistance < myDistance)
                    {
                        amITarget = false;
                        break;
                    }
                }

                if (!amITarget)
                    break;
            }

            if (amITarget)
            {
                var zookeeper = state.Zookeepers.FirstOrDefault();
                if (zookeeper != null)
                {
                    int currentDist = ManhattanDistance(zookeeper.X, zookeeper.Y, me.X, me.Y);
                    int newDist = ManhattanDistance(zookeeper.X, zookeeper.Y, nx, ny);

                    if (newDist > currentDist)
                        return 2.0m;
                    else if (newDist < currentDist)
                        return -2.0m;
                }
            }

            return 0m;
        }

        public static decimal SpawnProximity(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);

            var spawns = state.Animals.Select(a => (a.SpawnX, a.SpawnY)).Distinct().ToList();

            int minSpawnDist = spawns.Min(sp => ManhattanDistance(sp.SpawnX, sp.SpawnY, nx, ny));

            if (me.CapturedCounter > 0 && minSpawnDist <= 3)
                return 0.5m;

            return minSpawnDist <= 3 ? -1.0m : 0m;
        }

        public static decimal TimeToCapture(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);

            var zookeeper = state
                .Zookeepers.OrderBy(z => ManhattanDistance(z.X, z.Y, me.X, me.Y))
                .FirstOrDefault();

            if (zookeeper == null)
                return 0m;

            int timeEstimate = ManhattanDistance(zookeeper.X, zookeeper.Y, nx, ny);

            if (timeEstimate <= 2)
                return -2.0m;
            else if (timeEstimate <= 5)
                return -1.0m;
            else if (timeEstimate <= 10)
                return -0.5m;

            return 0.5m;
        }

        public static decimal EdgeSafety(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);

            int minX = state.Cells.Min(c => c.X);
            int maxX = state.Cells.Max(c => c.X);
            int minY = state.Cells.Min(c => c.Y);
            int maxY = state.Cells.Max(c => c.Y);

            int edgeDistance = Math.Min(
                Math.Min(nx - minX, maxX - nx),
                Math.Min(ny - minY, maxY - ny)
            );

            // More strongly penalize edge positions, especially when near edge
            if (edgeDistance == 0)
                return -2.0m; // Directly on edge (much stronger penalty)
            else if (edgeDistance == 1)
                return -0.8m; // Adjacent to edge
            else if (edgeDistance == 2)
                return -0.2m; // Near edge

            return 0m;
        }

        public static decimal QuadrantAwareness(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);

            int centerX = (state.Cells.Min(c => c.X) + state.Cells.Max(c => c.X)) / 2;
            int centerY = (state.Cells.Min(c => c.Y) + state.Cells.Max(c => c.Y)) / 2;

            int quadrant = (nx >= centerX ? 1 : 0) + (ny >= centerY ? 2 : 0);

            int[] pelletsByQuadrant = new int[4];
            foreach (var cell in state.Cells.Where(c => c.Content == CellContent.Pellet))
            {
                int q = (cell.X >= centerX ? 1 : 0) + (cell.Y >= centerY ? 2 : 0);
                pelletsByQuadrant[q]++;
            }

            int[] animalsByQuadrant = new int[4];
            foreach (var animal in state.Animals.Where(a => a.Id != me.Id && a.IsViable))
            {
                int q = (animal.X >= centerX ? 1 : 0) + (animal.Y >= centerY ? 2 : 0);
                animalsByQuadrant[q]++;
            }

            decimal quadrantValue = pelletsByQuadrant[quadrant] * 0.2m;
            if (animalsByQuadrant[quadrant] > 0)
                quadrantValue -= animalsByQuadrant[quadrant] * 0.1m;

            return quadrantValue;
        }

        public static decimal TargetEvaluation(GameState state, Animal me, BotAction m)
        {
            if (!state.Zookeepers.Any())
                return 0m;

            var (nx, ny) = ApplyMove(me.X, me.Y, m);

            var zookeeper = state.Zookeepers.First();
            var viableAnimals = state.Animals.Where(a => a.IsViable).ToList();

            if (!viableAnimals.Any())
                return 0m;

            int myDistance = ManhattanDistance(zookeeper.X, zookeeper.Y, me.X, me.Y);
            bool amITarget = true;

            foreach (var animal in viableAnimals.Where(a => a.Id != me.Id))
            {
                int theirDistance = ManhattanDistance(zookeeper.X, zookeeper.Y, animal.X, animal.Y);
                if (theirDistance < myDistance)
                {
                    amITarget = false;
                    break;
                }
            }

            if (amITarget)
            {
                int newDist = ManhattanDistance(zookeeper.X, zookeeper.Y, nx, ny);
                return newDist > myDistance ? 1.5m : -1.5m;
            }
            else
            {
                return 0.5m;
            }
        }

        public static decimal TiebreakersAwareness(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);

            decimal value = 0m;

            if (nx != me.X || ny != me.Y)
                value += 0.1m;

            if (me.X == me.SpawnX && me.Y == me.SpawnY)
                value += 2.0m;

            return value;
        }

        public static decimal ZookeeperCooldown(GameState state, Animal me, BotAction m)
        {
            int recalcTick = state.Tick % 20;

            if (recalcTick >= 18 || recalcTick == 0)
            {
                var zookeeper = state.Zookeepers.FirstOrDefault();
                if (zookeeper != null)
                {
                    var (nx, ny) = ApplyMove(me.X, me.Y, m);
                    int currentDist = ManhattanDistance(zookeeper.X, zookeeper.Y, me.X, me.Y);
                    int newDist = ManhattanDistance(zookeeper.X, zookeeper.Y, nx, ny);

                    if (newDist > currentDist)
                        return 1.5m;
                }
            }

            return 0m;
        }

        public static decimal PelletEfficiency(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);

            int pelletsInRange = 0;
            foreach (var cell in state.Cells.Where(c => c.Content == CellContent.Pellet))
            {
                int dist = ManhattanDistance(cell.X, cell.Y, nx, ny);
                if (dist <= 3)
                    pelletsInRange++;
            }

            decimal risk = 1.0m;
            if (state.Zookeepers.Any())
            {
                int minDist = state.Zookeepers.Min(z => ManhattanDistance(z.X, z.Y, nx, ny));
                risk = Math.Max(1.0m, minDist);
            }

            return pelletsInRange / risk;
        }

        public static decimal EscapeRoutes(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);

            int escapeCount = 0;
            foreach (BotAction action in Enum.GetValues<BotAction>())
            {
                var (ex, ey) = ApplyMove(nx, ny, action);
                if (IsTraversable(state, ex, ey))
                {
                    if (state.Zookeepers.Any())
                    {
                        var zookeeper = state
                            .Zookeepers.OrderBy(z => ManhattanDistance(z.X, z.Y, nx, ny))
                            .First();

                        int currentDist = ManhattanDistance(zookeeper.X, zookeeper.Y, nx, ny);
                        int escapeDist = ManhattanDistance(zookeeper.X, zookeeper.Y, ex, ey);

                        if (escapeDist > currentDist)
                            escapeCount++;
                    }
                    else
                    {
                        escapeCount++;
                    }
                }
            }

            return escapeCount * 0.3m;
        }

        public static decimal AnimalCongestion(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);

            int competitorCount = 0;
            foreach (var animal in state.Animals.Where(a => a.Id != me.Id && a.IsViable))
            {
                int dist = ManhattanDistance(animal.X, animal.Y, nx, ny);
                if (dist <= 3)
                    competitorCount++;
            }

            return -competitorCount * 0.4m;
        }

        public static decimal CaptureRecoveryStrategy(GameState state, Animal me, BotAction m)
        {
            if (me.X == me.SpawnX && me.Y == me.SpawnY)
            {
                var (nx, ny) = ApplyMove(me.X, me.Y, m);

                int pelletCount = 0;
                int competitorCount = 0;

                foreach (var cell in state.Cells.Where(c => c.Content == CellContent.Pellet))
                {
                    bool inDirection = false;

                    switch (m)
                    {
                        case BotAction.Up:
                            inDirection =
                                cell.Y < me.Y && Math.Abs(cell.X - me.X) < Math.Abs(cell.Y - me.Y);
                            break;
                        case BotAction.Down:
                            inDirection =
                                cell.Y > me.Y && Math.Abs(cell.X - me.X) < Math.Abs(cell.Y - me.Y);
                            break;
                        case BotAction.Left:
                            inDirection =
                                cell.X < me.X && Math.Abs(cell.X - me.X) > Math.Abs(cell.Y - me.Y);
                            break;
                        case BotAction.Right:
                            inDirection =
                                cell.X > me.X && Math.Abs(cell.X - me.X) > Math.Abs(cell.Y - me.Y);
                            break;
                    }

                    if (inDirection && ManhattanDistance(cell.X, cell.Y, nx, ny) <= 5)
                        pelletCount++;
                }

                foreach (var animal in state.Animals.Where(a => a.Id != me.Id && a.IsViable))
                {
                    bool inDirection = false;

                    switch (m)
                    {
                        case BotAction.Up:
                            inDirection = animal.Y < me.Y;
                            break;
                        case BotAction.Down:
                            inDirection = animal.Y > me.Y;
                            break;
                        case BotAction.Left:
                            inDirection = animal.X < me.X;
                            break;
                        case BotAction.Right:
                            inDirection = animal.X > me.X;
                            break;
                    }

                    if (inDirection && ManhattanDistance(animal.X, animal.Y, nx, ny) <= 5)
                        competitorCount++;
                }

                return pelletCount * 0.4m - competitorCount * 0.5m;
            }

            return 0m;
        }

        // New winning heuristics implementation

        /// <summary>
        /// Prioritizes immediate action to gain advantage in the first command issued tiebreaker
        /// </summary>
        public static decimal FirstCommandAdvantage(GameState state, Animal me, BotAction m)
        {
            // If this is near the start of the game and we're at spawn, prioritize any valid move
            if (state.Tick < 5 && me.X == me.SpawnX && me.Y == me.SpawnY)
            {
                var (nx, ny) = ApplyMove(me.X, me.Y, m);
                if (IsTraversable(state, nx, ny))
                    return 2.0m;
            }
            return 0m;
        }

        /// <summary>
        /// Maximizes total distance traveled for tiebreaker advantage
        /// </summary>
        public static decimal TravelDistanceMaximizer(GameState state, Animal me, BotAction m)
        {
            // Moving is always better than staying still to maximize traveled distance
            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            if (nx != me.X || ny != me.Y)
                return 0.3m;

            return 0m;
        }

        /// <summary>
        /// Minimizes time spent on spawn for tiebreaker advantage
        /// </summary>
        public static decimal SpawnTimeMinimizer(GameState state, Animal me, BotAction m)
        {
            // Highest priority to move away from spawn if we're on it
            if (me.X == me.SpawnX && me.Y == me.SpawnY)
            {
                var (nx, ny) = ApplyMove(me.X, me.Y, m);
                if (nx != me.X || ny != me.Y)
                    return 3.0m;
            }
            return 0m;
        }

        /// <summary>
        /// Adjusts strategy based on game timer
        /// </summary>
        public static decimal TimerAwareness(GameState state, Animal me, BotAction m)
        {
            decimal value = 0m;
            int maxTicks = 600; // Adjust based on game settings
            float gameProgress = (float)state.Tick / maxTicks;

            // Early game: Focus on securing pellets and avoiding capture
            if (gameProgress < 0.3f)
            {
                // Evaluate pellet availability
                var (nx, ny) = ApplyMove(me.X, me.Y, m);
                int pelletsNearby = state.Cells.Count(c =>
                    c.Content == CellContent.Pellet && ManhattanDistance(c.X, c.Y, nx, ny) <= 3
                );

                value += pelletsNearby * 0.2m;
            }
            // Mid game: Balance pellet collection with strategic positioning
            else if (gameProgress < 0.7f)
            {
                // Adjust risk tolerance
                if (state.Animals.OrderByDescending(a => a.Score).First().Id != me.Id)
                {
                    // If not leading, take more risks
                    value += HeuristicsImpl.ResourceClustering(state, me, m) * 0.5m;
                }
            }
            // Late game: Focus on maintaining lead or catching up
            else
            {
                // If leading, focus on safety
                if (state.Animals.OrderByDescending(a => a.Score).First().Id == me.Id)
                {
                    var (nx, ny) = ApplyMove(me.X, me.Y, m);
                    int zooMinDist = state.Zookeepers.Any()
                        ? state.Zookeepers.Min(z => ManhattanDistance(z.X, z.Y, nx, ny))
                        : 999;

                    if (zooMinDist > 5)
                        value += 0.8m;
                }
                // If not leading, focus on pellets
                else
                {
                    value += HeuristicsImpl.ResourceClustering(state, me, m) * 0.8m;
                }
            }

            return value;
        }

        /// <summary>
        /// Adjusts strategy based on percentage of pellets remaining
        /// </summary>
        public static decimal PelletRatioAwareness(GameState state, Animal me, BotAction m)
        {
            // Estimate total map size and initial pellet count
            int minX = state.Cells.Min(c => c.X);
            int maxX = state.Cells.Max(c => c.X);
            int minY = state.Cells.Min(c => c.Y);
            int maxY = state.Cells.Max(c => c.Y);
            int mapArea = (maxX - minX + 1) * (maxY - minY + 1);

            // Estimate walls/obstacles as 20% of map
            int estimatedInitialPellets = (int)(mapArea * 0.8);
            int currentPellets = state.Cells.Count(c => c.Content == CellContent.Pellet);

            float pelletRatio = (float)currentPellets / estimatedInitialPellets;

            // Strategy adjustments based on remaining pellets
            if (pelletRatio < 0.2f) // End game, few pellets
            {
                var (nx, ny) = ApplyMove(me.X, me.Y, m);

                // Prioritize moves toward remaining pellets
                var closestPellet = state
                    .Cells.Where(c => c.Content == CellContent.Pellet)
                    .OrderBy(c => ManhattanDistance(c.X, c.Y, nx, ny))
                    .FirstOrDefault();

                if (closestPellet != null)
                {
                    int currentDist = ManhattanDistance(
                        me.X,
                        me.Y,
                        closestPellet.X,
                        closestPellet.Y
                    );
                    int newDist = ManhattanDistance(nx, ny, closestPellet.X, closestPellet.Y);

                    if (newDist < currentDist)
                        return 2.0m;
                }
            }

            return 0m;
        }

        /// <summary>
        /// Optimizes command queue usage for strategic advantage
        /// </summary>
        public static decimal CommandQueueOptimization(GameState state, Animal me, BotAction m)
        {
            // Simulate what happens if we take this move
            var (nx, ny) = ApplyMove(me.X, me.Y, m);

            // Look for pellets along path of continuous movement
            int pelletCount = 0;
            int lookAhead = 3; // How many cells to look ahead
            int x = nx,
                y = ny;

            for (int i = 0; i < lookAhead; i++)
            {
                // Apply the same move repeatedly to predict path
                (x, y) = ApplyMove(x, y, m);

                // Stop if we hit a wall
                if (!IsTraversable(state, x, y))
                    break;

                // Count pellets along projected path
                if (state.Cells.Any(c => c.X == x && c.Y == y && c.Content == CellContent.Pellet))
                    pelletCount++;
            }

            return pelletCount * 0.4m;
        }

        /// <summary>
        /// Anticipates other animals' moves to avoid competition for the same pellets
        /// </summary>
        public static decimal AnticipateCompetition(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);

            // Check for pellets in our target location
            bool hasPellet = state.Cells.Any(c =>
                c.X == nx && c.Y == ny && c.Content == CellContent.Pellet
            );

            if (hasPellet)
            {
                // Count animals that might be competing for the same pellet
                int competitors = 0;
                foreach (var animal in state.Animals.Where(a => a.Id != me.Id && a.IsViable))
                {
                    // Check if they're adjacent to the pellet
                    if (ManhattanDistance(animal.X, animal.Y, nx, ny) == 1)
                    {
                        competitors++;
                    }
                }

                // Penalize moves toward contested pellets
                if (competitors > 0)
                    return -competitors * 0.5m;
            }

            return 0m;
        }

        /// <summary>
        /// Implements special behavior for the endgame when few pellets remain
        /// </summary>
        public static decimal EndgameStrategy(GameState state, Animal me, BotAction m)
        {
            // Get the current pellet count
            int pelletCount = state.Cells.Count(c => c.Content == CellContent.Pellet);

            // Define endgame as less than 10% of pellets remaining
            int minX = state.Cells.Min(c => c.X);
            int maxX = state.Cells.Max(c => c.X);
            int minY = state.Cells.Min(c => c.Y);
            int maxY = state.Cells.Max(c => c.Y);
            int mapArea = (maxX - minX + 1) * (maxY - minY + 1);
            int estimatedInitialPellets = (int)(mapArea * 0.8);

            if (pelletCount < estimatedInitialPellets * 0.1)
            {
                var (nx, ny) = ApplyMove(me.X, me.Y, m);

                // Find closest remaining pellet
                var closestPellet = state
                    .Cells.Where(c => c.Content == CellContent.Pellet)
                    .OrderBy(c => ManhattanDistance(c.X, c.Y, nx, ny))
                    .FirstOrDefault();

                if (closestPellet != null)
                {
                    // Heavily prioritize moving toward closest pellet
                    int currentDist = ManhattanDistance(
                        me.X,
                        me.Y,
                        closestPellet.X,
                        closestPellet.Y
                    );
                    int newDist = ManhattanDistance(nx, ny, closestPellet.X, closestPellet.Y);

                    if (newDist < currentDist)
                        return 3.0m;
                    else if (newDist > currentDist)
                        return -2.0m;
                }
            }

            return 0m;
        }

        /// <summary>
        /// Focuses on controlling high-value areas with multiple pellets
        /// </summary>
        public static decimal PositionalDominance(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);

            // Define radius to check for pellet clusters
            int radius = 3;

            // Count pellets within radius
            int pelletCount = state.Cells.Count(c =>
                c.Content == CellContent.Pellet && ManhattanDistance(c.X, c.Y, nx, ny) <= radius
            );

            // Count other animals within radius
            int animalCount = state.Animals.Count(a =>
                a.Id != me.Id && a.IsViable && ManhattanDistance(a.X, a.Y, nx, ny) <= radius
            );

            // Calculate value of this position (pellets - competition)
            decimal positionValue = pelletCount * 0.3m - animalCount * 0.4m;

            // Bonus for being in a better position than current
            int currentPellets = state.Cells.Count(c =>
                c.Content == CellContent.Pellet && ManhattanDistance(c.X, c.Y, me.X, me.Y) <= radius
            );

            if (pelletCount > currentPellets)
                positionValue += 0.5m;

            return positionValue;
        }

        /// <summary>
        /// Calculates risk/reward considering score loss from capture
        /// </summary>
        public static decimal ScoreLossMinimizer(GameState state, Animal me, BotAction m)
        {
            // Assume capture penalty is 50% of score (adjust based on actual game settings)
            float capturePenaltyPercent = 0.5f;

            // Calculate potential score loss if captured
            decimal potentialLoss = me.Score * (decimal)capturePenaltyPercent;

            var (nx, ny) = ApplyMove(me.X, me.Y, m);

            // Calculate capture risk based on zookeeper proximity
            decimal captureRisk = 0m;
            if (state.Zookeepers.Any())
            {
                int minDist = state.Zookeepers.Min(z => ManhattanDistance(z.X, z.Y, nx, ny));

                // Risk increases as zookeeper gets closer
                if (minDist <= 3)
                    captureRisk = 0.8m / (minDist + 0.5m);
                else if (minDist <= 6)
                    captureRisk = 0.3m / (minDist + 0.5m);
            }

            // Calculate risk-adjusted value
            decimal scoreThreshold = 20m; // Significant score worth protecting

            if (potentialLoss > scoreThreshold)
            {
                // Increase caution as potential loss increases
                return -captureRisk * (potentialLoss / 10m);
            }

            return -captureRisk * 0.5m; // Lower caution for small scores
        }

        /// <summary>
        /// Detects and avoids repetitive movement patterns (cycles)
        /// </summary>
        public static decimal CycleDetection(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            string animalKey = me.Id.ToString();

            // Check if we have position history for this animal
            if (!_recentPositions.ContainsKey(animalKey))
                return 0m;

            var positions = _recentPositions[animalKey];

            // Don't penalize if we're just starting out
            if (positions.Count < 3)
                return 0m;

            // Check how many times we've visited this position recently
            int visitCount = positions.Count(p => p.Item1 == nx && p.Item2 == ny);

            // Strongly penalize revisiting the same position multiple times
            if (visitCount >= 2)
                return -3.0m * visitCount;

            // Detect simple cycles (ping-ponging between 2 cells)
            if (positions.Count >= 4)
            {
                var positionList = positions.ToList();
                for (int i = 0; i < positionList.Count - 3; i += 2)
                {
                    // Check if we're alternating between the same two cells
                    if (
                        positionList[i].Item1 == positionList[i + 2].Item1
                        && positionList[i].Item2 == positionList[i + 2].Item2
                        && positionList[i + 1].Item1 == positionList[i + 3].Item1
                        && positionList[i + 1].Item2 == positionList[i + 3].Item2
                    )
                    {
                        // If the move would continue the cycle, strongly penalize it
                        if (
                            (nx == positionList[i].Item1 && ny == positionList[i].Item2)
                            || (nx == positionList[i + 1].Item1 && ny == positionList[i + 1].Item2)
                        )
                            return -4.0m;
                    }
                }
            }

            // Detect larger cycles (3 or 4 cells)
            if (positions.Count >= 8)
            {
                var lastFew = positions.Skip(positions.Count - 4).ToList();
                bool matchFound = true;

                for (int i = 0; i < Math.Min(4, lastFew.Count); i++)
                {
                    int prevIndex = positions.Count - 8 + i;
                    if (
                        prevIndex < 0
                        || prevIndex >= positions.Count
                        || (
                            positions.ElementAt(prevIndex).Item1 != lastFew[i].Item1
                            || positions.ElementAt(prevIndex).Item2 != lastFew[i].Item2
                        )
                    )
                    {
                        matchFound = false;
                        break;
                    }
                }

                if (matchFound)
                {
                    // We're in a repeating cycle, strongly penalize moves that continue it
                    foreach (var pos in lastFew)
                    {
                        if (nx == pos.Item1 && ny == pos.Item2)
                            return -5.0m;
                    }
                }
            }

            return 0m;
        }

        /// <summary>
        /// Encourages variety in direction choices to avoid getting stuck
        /// </summary>
        public static decimal DirectionalVariety(GameState state, Animal me, BotAction m)
        {
            string animalKey = me.Id.ToString();

            // If we don't have a last direction, record this one and return neutral score
            if (!_lastDirections.TryGetValue(animalKey, out BotAction lastDirection))
            {
                _lastDirections[animalKey] = m;
                return 0m;
            }

            // Penalty for reversing direction completely (ping-ponging)
            if (
                (m == BotAction.Up && lastDirection == BotAction.Down)
                || (m == BotAction.Down && lastDirection == BotAction.Up)
                || (m == BotAction.Left && lastDirection == BotAction.Right)
                || (m == BotAction.Right && lastDirection == BotAction.Left)
            )
            {
                return -2.0m;
            }

            // Slight bonus for changing direction (not reversing, but turning)
            if (m != lastDirection)
            {
                _lastDirections[animalKey] = m;
                return 0.5m;
            }

            // Continuing in same direction gets neutral score
            _lastDirections[animalKey] = m;
            return 0m;
        }

        /// <summary>
        /// Avoids empty cells when there are pellets nearby
        /// </summary>
        public static decimal EmptyCellAvoidance(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);

            // Check if target cell has a pellet
            bool hasPellet = state.Cells.Any(c =>
                c.X == nx && c.Y == ny && c.Content == CellContent.Pellet
            );

            // If moving to an empty cell, check if there are nearby pellets we should prioritize
            if (!hasPellet)
            {
                // Look for pellets in a small radius
                int searchRadius = 3;
                var nearbyPellets = state.Cells.Where(c =>
                    c.Content == CellContent.Pellet
                    && ManhattanDistance(c.X, c.Y, me.X, me.Y) <= searchRadius
                );

                // If we're ignoring nearby pellets, penalize this move
                if (nearbyPellets.Any())
                {
                    // Count how many pellets are nearby
                    int pelletCount = nearbyPellets.Count();

                    // Get direction to closest pellet
                    var closestPellet = nearbyPellets
                        .OrderBy(c => ManhattanDistance(c.X, c.Y, me.X, me.Y))
                        .FirstOrDefault();

                    // Determine if we're moving toward or away from the closest pellet
                    if (closestPellet != null)
                    {
                        int currentDist = ManhattanDistance(
                            me.X,
                            me.Y,
                            closestPellet.X,
                            closestPellet.Y
                        );
                        int newDist = ManhattanDistance(nx, ny, closestPellet.X, closestPellet.Y);

                        // Penalize moving to empty cells when we're moving away from nearby pellets
                        if (newDist > currentDist)
                            return -1.0m * pelletCount;
                    }
                }
            }

            return 0m;
        }

        /// <summary>
        /// Encourages moving closer to other animals when this animal is the closest to a zookeeper
        /// </summary>
        public static decimal AnimalProximityWhenTargeted(GameState state, Animal me, BotAction m)
        {
            // If there are no zookeepers or other animals, don't apply this heuristic
            if (
                !state.Zookeepers.Any()
                || state.Animals.Count(a => a.IsViable && a.Id != me.Id) == 0
            )
                return 0m;

            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            bool amITargeted = false;

            // Check if I'm the closest animal to any zookeeper
            foreach (var zookeeper in state.Zookeepers)
            {
                int myDistance = ManhattanDistance(zookeeper.X, zookeeper.Y, me.X, me.Y);
                bool someoneIsCloser = false;

                foreach (var animal in state.Animals.Where(a => a.IsViable && a.Id != me.Id))
                {
                    int theirDistance = ManhattanDistance(
                        zookeeper.X,
                        zookeeper.Y,
                        animal.X,
                        animal.Y
                    );
                    if (theirDistance < myDistance)
                    {
                        someoneIsCloser = true;
                        break;
                    }
                }

                if (!someoneIsCloser)
                {
                    amITargeted = true;
                    break;
                }
            }

            // If I'm not targeted by any zookeeper, this heuristic doesn't apply
            if (!amITargeted)
                return 0m;

            // Find the closest animal to my new position
            int closestAnimalDistance = int.MaxValue;
            foreach (var animal in state.Animals.Where(a => a.IsViable && a.Id != me.Id))
            {
                int distance = ManhattanDistance(nx, ny, animal.X, animal.Y);
                closestAnimalDistance = Math.Min(closestAnimalDistance, distance);
            }

            // Reward moves that bring us closer to other animals when we're targeted
            // The closer we get to other animals, the better
            if (closestAnimalDistance <= 1)
                return 3.0m; // Strongly reward being adjacent to another animal
            else
                return 5.0m / (closestAnimalDistance + 1); // Diminishing reward for proximity
        }
    }
}
