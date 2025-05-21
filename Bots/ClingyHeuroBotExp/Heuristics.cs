using System.Collections.Concurrent;
using DeepMCTS.Enums;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace HeuroBot.Services;

public static class Heuristics
{
    // Keep track of recently visited positions to avoid cycles
    private static readonly ConcurrentDictionary<string, Queue<(int, int)>> _recentPositions =
        new ConcurrentDictionary<string, Queue<(int, int)>>();

    // Track visit counts for each position to apply increasing penalties
    private static readonly ConcurrentDictionary<string, Dictionary<(int, int), int>> _visitCounts =
        new ConcurrentDictionary<string, Dictionary<(int, int), int>>();

    // Track previous positions specifically (most recent first)
    private static readonly ConcurrentDictionary<string, List<(int, int)>> _previousPositions =
        new ConcurrentDictionary<string, List<(int, int)>>();

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

        // Initialize or get visit counts for this animal
        if (!_visitCounts.ContainsKey(animalKey))
        {
            _visitCounts[animalKey] = new Dictionary<(int, int), int>();
        }

        // Initialize or get previous positions list
        if (!_previousPositions.ContainsKey(animalKey))
        {
            _previousPositions[animalKey] = new List<(int, int)>();
        }

        // Add current position to history
        var positions = _recentPositions[animalKey];
        positions.Enqueue((me.X, me.Y));

        // Update visit counts for the current position
        var visitCountMap = _visitCounts[animalKey];
        var currentPos = (me.X, me.Y);
        if (visitCountMap.ContainsKey(currentPos))
        {
            visitCountMap[currentPos]++;
        }
        else
        {
            visitCountMap[currentPos] = 1;
        }

        // Update the previous positions list (most recent first)
        var prevPositions = _previousPositions[animalKey];
        prevPositions.Insert(0, currentPos); // Add current position at the beginning
        while (prevPositions.Count > 5) // Keep last 5 positions
        {
            prevPositions.RemoveAt(prevPositions.Count - 1);
        }

        // Keep only the most recent 15 positions to detect cycles
        while (positions.Count > 15)
        {
            positions.Dequeue();
        }

        // Clear history if animal was just captured (at spawn point)
        if (me.X == me.SpawnX && me.Y == me.SpawnY)
        {
            positions.Clear();
            _lastDirections.TryRemove(animalKey, out _);
            _visitCounts[animalKey].Clear(); // Reset visit counts after capture
            _previousPositions[animalKey].Clear(); // Reset previous positions after capture
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
        // Command queue optimization removed to focus on single best move selection
        score +=
            HeuristicsImpl.AnticipateCompetition(state, me, move) * WEIGHTS.AnticipateCompetition;
        score += HeuristicsImpl.EndgameStrategy(state, me, move) * WEIGHTS.EndgameStrategy;
        score += HeuristicsImpl.PositionalDominance(state, me, move) * WEIGHTS.PositionalDominance;
        score += HeuristicsImpl.ScoreLossMinimizer(state, me, move) * WEIGHTS.ScoreLossMinimizer;

        // Add the previous position penalty (very high priority)
        score += HeuristicsImpl.PreviousPositionPenalty(state, me, move) * 5.0m; // High weight for this

        // Add movement consistency bonus
        score += HeuristicsImpl.MovementConsistency(state, me, move) * 2.0m;

        // Add optimal pellet path efficiency to minimize empty cell movements
        score += HeuristicsImpl.OptimalPelletPathEfficiency(state, me, move) * 4.0m; // High weight

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

            // Check if we're moving onto a pellet (immediate reward)
            if (pellets.Any(p => p.X == nx && p.Y == ny))
                return 5.0m; // Significant reward for directly collecting a pellet

            // Get the closest pellet using optimized path finding
            var closestPellet = FindClosestPellet(state, nx, ny);
            if (closestPellet == null)
                return 0m;

            // Calculate distances using optimal path length, not just Manhattan distance
            int currentDist = FindShortestPathLength(
                state,
                me.X,
                me.Y,
                closestPellet.X,
                closestPellet.Y
            );
            int newDist = FindShortestPathLength(state, nx, ny, closestPellet.X, closestPellet.Y);

            // Very heavily reward moves that get us closer to the nearest pellet
            if (newDist < currentDist)
                return 3.0m; // Increased from 2.0m to prioritize optimal routes
            else if (newDist > currentDist)
                return -2.0m; // Increased penalty to avoid non-optimal moves

            // If the distance stays the same, check if we're at least moving in the general direction
            int directionalImprovement = IsMovingInCorrectDirection(
                me.X,
                me.Y,
                closestPellet.X,
                closestPellet.Y,
                m
            );
            if (directionalImprovement > 0)
                return 0.5m;

            return 0m;
        }

        private static Cell FindClosestPellet(GameState state, int x, int y)
        {
            var pellets = state.Cells.Where(c => c.Content == CellContent.Pellet);
            if (!pellets.Any())
                return null;

            // First try a quick check with Manhattan distance to find potential candidates
            var closestPellets = pellets
                .OrderBy(p => ManhattanDistance(x, y, p.X, p.Y))
                .Take(3) // Take a few candidates to evaluate more thoroughly
                .ToList();

            if (closestPellets.Count == 1)
                return closestPellets[0];

            // For multiple candidates, find the truly closest by path length
            return closestPellets
                .OrderBy(p => FindShortestPathLength(state, x, y, p.X, p.Y))
                .FirstOrDefault();
        }

        private static int FindShortestPathLength(
            GameState state,
            int startX,
            int startY,
            int goalX,
            int goalY
        )
        {
            // If start and goal are the same, return 0
            if (startX == goalX && startY == goalY)
                return 0;

            // A simple breadth-first search to find shortest path length
            var visited = new HashSet<(int, int)>();
            var queue = new Queue<(int x, int y, int dist)>();
            queue.Enqueue((startX, startY, 0));
            visited.Add((startX, startY));

            while (queue.Count > 0)
            {
                var (x, y, dist) = queue.Dequeue();

                // Check all four directions
                foreach (
                    var direction in new[]
                    {
                        BotAction.Up,
                        BotAction.Down,
                        BotAction.Left,
                        BotAction.Right,
                    }
                )
                {
                    var (nx, ny) = ApplyMove(x, y, direction);

                    // Skip if we've already visited or if it's not traversable
                    if (!IsTraversable(state, nx, ny) || visited.Contains((nx, ny)))
                        continue;

                    // Found the goal
                    if (nx == goalX && ny == goalY)
                        return dist + 1;

                    // Add to queue for further exploration
                    queue.Enqueue((nx, ny, dist + 1));
                    visited.Add((nx, ny));
                }
            }

            // If no path found, return Manhattan distance as an estimate
            return ManhattanDistance(startX, startY, goalX, goalY);
        }

        private static int IsMovingInCorrectDirection(
            int x,
            int y,
            int goalX,
            int goalY,
            BotAction move
        )
        {
            switch (move)
            {
                case BotAction.Up:
                    return y > goalY ? 1 : -1;
                case BotAction.Down:
                    return y < goalY ? 1 : -1;
                case BotAction.Left:
                    return x > goalX ? 1 : -1;
                case BotAction.Right:
                    return x < goalX ? 1 : -1;
                default:
                    return 0;
            }
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

            // Check for pellets at different distances with weighted scoring
            decimal clusterScore = 0m;

            // Calculate distance-weighted scores for pellets in various radiuses
            // Immediate pellets (radius 1) get highest weight
            var immediatePellets = state
                .Cells.Where(c =>
                    c.Content == CellContent.Pellet && ManhattanDistance(c.X, c.Y, nx, ny) <= 1
                )
                .ToList();
            clusterScore += immediatePellets.Count * 4.0m;

            // Nearby pellets (radius 2) get good weight
            var nearbyPellets = state
                .Cells.Where(c =>
                    c.Content == CellContent.Pellet
                    && ManhattanDistance(c.X, c.Y, nx, ny) > 1
                    && ManhattanDistance(c.X, c.Y, nx, ny) <= 2
                )
                .ToList();
            clusterScore += nearbyPellets.Count * 2.0m;

            // Moderately close pellets (radius 3-4) still contribute
            var moderatePellets = state
                .Cells.Where(c =>
                    c.Content == CellContent.Pellet
                    && ManhattanDistance(c.X, c.Y, nx, ny) > 2
                    && ManhattanDistance(c.X, c.Y, nx, ny) <= 4
                )
                .ToList();
            clusterScore += moderatePellets.Count * 1.0m;

            // Detect dense clusters by analyzing pellet proximity to each other
            decimal densityBonus = 0m;
            var allRelevantPellets = immediatePellets
                .Concat(nearbyPellets)
                .Concat(moderatePellets)
                .ToList();

            // Bonus for densely packed pellets (pellets that are adjacent to other pellets)
            foreach (var pellet in allRelevantPellets)
            {
                int adjacentPellets = allRelevantPellets.Count(p =>
                    p != pellet && ManhattanDistance(p.X, p.Y, pellet.X, pellet.Y) <= 1
                );

                // Strong bonus for pellets that are part of a cluster
                if (adjacentPellets > 0)
                {
                    densityBonus += adjacentPellets * 0.5m;
                }
            }

            // Add substantial bonus if we're heading towards a dense pellet cluster
            return clusterScore + densityBonus;
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

            // Check if there's a pellet on or very close to the edge we're moving toward
            bool pelletNearEdge = state.Cells.Any(c =>
                c.Content == CellContent.Pellet
                && ManhattanDistance(c.X, c.Y, nx, ny) <= 3
                && Math.Min(Math.Min(c.X - minX, maxX - c.X), Math.Min(c.Y - minY, maxY - c.Y)) <= 2
            );

            // If we're moving toward a pellet near an edge, reduce or eliminate the edge penalty
            if (pelletNearEdge)
            {
                // Direct pellet on path completely eliminates edge penalty
                bool directPellet = state.Cells.Any(c =>
                    c.Content == CellContent.Pellet && c.X == nx && c.Y == ny
                );

                if (directPellet)
                    return 0m; // No penalty if we're moving onto a pellet at the edge

                // Moving toward a pellet near edge - reduced penalty
                if (edgeDistance == 0)
                    return -0.5m; // Reduced penalty
                else
                    return 0m; // No penalty if not directly on edge
            }

            // No pellets near edge, apply normal edge safety penalties
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

        // CommandQueueOptimization function removed to focus on single best move selection

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
        /// Detects and avoids repetitive movement patterns (cycles) with progressive penalties
        /// </summary>
        public static decimal CycleDetection(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            string animalKey = me.Id.ToString();

            // Check if we have position history for this animal
            if (!_recentPositions.ContainsKey(animalKey) || !_visitCounts.ContainsKey(animalKey))
                return 0m;

            var positions = _recentPositions[animalKey];
            var visitCountMap = _visitCounts[animalKey];
            var targetPos = (nx, ny);

            // Don't penalize if we're just starting out
            if (positions.Count < 3)
                return 0m;

            // Progressive penalties based on visit count - exponentially increase penalties
            int visitCount = visitCountMap.ContainsKey(targetPos) ? visitCountMap[targetPos] : 0;

            if (visitCount > 0)
            {
                // Calculate exponentially increasing penalty based on visit count
                // First visit: no penalty, second: -2, third: -4, fourth: -8, etc.
                // Using a safe approach to avoid decimal overflow
                decimal penalty;

                if (visitCount <= 3)
                {
                    // Safe direct calculation for small values
                    penalty = (decimal)Math.Pow(2, visitCount) * -1.0m;
                }
                else
                {
                    // For larger values, just use a large fixed penalty
                    // This prevents decimal overflow
                    penalty = -10.0m;
                }

                return penalty;
            }

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
                            return -5.0m; // Increased from -4.0m
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
                            return -6.0m; // Increased from -5.0m
                    }
                }
            }

            // Check if moving to an empty cell that's been visited many times
            bool hasPellet = state.Cells.Any(c =>
                c.X == nx && c.Y == ny && c.Content == CellContent.Pellet
            );
            if (!hasPellet && visitCount > 1)
            {
                // Higher penalty for repeatedly visiting empty cells
                return -1.0m * visitCount;
            }

            return 0m;
        }

        /// <summary>
        /// Encourages variety in direction choices to avoid getting stuck, with edge awareness
        /// </summary>
        public static decimal DirectionalVariety(GameState state, Animal me, BotAction m)
        {
            string animalKey = me.Id.ToString();
            var (nx, ny) = ApplyMove(me.X, me.Y, m);

            // If we don't have a last direction, record this one and return neutral score
            if (!_lastDirections.TryGetValue(animalKey, out BotAction lastDirection))
            {
                _lastDirections[animalKey] = m;
                return 0m;
            }

            // Get map boundaries to detect edges
            int minX = state.Cells.Min(c => c.X);
            int maxX = state.Cells.Max(c => c.X);
            int minY = state.Cells.Min(c => c.Y);
            int maxY = state.Cells.Max(c => c.Y);

            // Check if we're at an edge
            bool atVerticalEdge = nx == minX || nx == maxX;
            bool atHorizontalEdge = ny == minY || ny == maxY;

            // Check if there's a pellet at our destination
            bool movingToPellet = state.Cells.Any(c =>
                c.X == nx && c.Y == ny && c.Content == CellContent.Pellet
            );

            // Penalty for reversing direction completely (ping-ponging)
            bool isReversal =
                (m == BotAction.Up && lastDirection == BotAction.Down)
                || (m == BotAction.Down && lastDirection == BotAction.Up)
                || (m == BotAction.Left && lastDirection == BotAction.Right)
                || (m == BotAction.Right && lastDirection == BotAction.Left);

            if (isReversal)
            {
                // Reduce reversal penalty if we're at an edge and have limited options
                // This helps the bot escape from edge traps
                if (
                    (atVerticalEdge && (m == BotAction.Left || m == BotAction.Right))
                    || (atHorizontalEdge && (m == BotAction.Up || m == BotAction.Down))
                )
                {
                    // Almost no penalty for reversing at an edge since options are limited
                    return -0.1m;
                }

                // Allow reversal with reduced penalty if it leads to a pellet
                if (movingToPellet)
                {
                    return -0.5m; // Much smaller penalty for reversing to get a pellet
                }

                // Standard high penalty for reversals in open space
                return -2.0m;
            }

            // Bonus for changing direction (not reversing, but turning)
            if (m != lastDirection)
            {
                _lastDirections[animalKey] = m;

                // Extra bonus if turning leads to a pellet
                if (movingToPellet)
                    return 1.0m;

                return 0.5m;
            }

            // Continuing in same direction
            _lastDirections[animalKey] = m;

            // Bonus for continuing in same direction if it leads to a pellet
            if (movingToPellet)
                return 0.5m;

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

            // Strongly reward being adjacent to other animals (protection position)
            if (closestAnimalDistance <= 1)
                return 5.0m; // Significantly increased from 3.0m to prioritize positioning next to other animals
            else if (closestAnimalDistance <= 2)
                return 3.5m; // High reward for being 2 steps away
            else if (closestAnimalDistance <= 3)
                return 2.5m; // Good reward for being 3 steps away

            if (closestAnimalDistance <= 5)
                return 1.0m;
            else
                return 8.0m / (closestAnimalDistance + 1); // Stronger diminishing reward for proximity
        }

        /// <summary>
        /// Applies a strong penalty for going back to the immediately previous position
        /// </summary>
        public static decimal PreviousPositionPenalty(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            string animalKey = me.Id.ToString();

            // If we don't have previous positions, can't apply this heuristic
            if (
                !_previousPositions.ContainsKey(animalKey)
                || _previousPositions[animalKey].Count < 2
            )
                return 0m;

            var prevPositions = _previousPositions[animalKey];

            // Check if the move would take us back to the previous position (not current, but one before)
            if (prevPositions.Count >= 2)
            {
                var prevPos = prevPositions[1]; // Index 1 is the previous position (0 is current)

                if (nx == prevPos.Item1 && ny == prevPos.Item2)
                {
                    // Check if there's a pellet there
                    bool hasPellet = state.Cells.Any(c =>
                        c.X == nx && c.Y == ny && c.Content == CellContent.Pellet
                    );

                    if (hasPellet)
                        return -3.0m; // Smaller penalty if there's a pellet there (still bad but might be worth it)
                    else
                        return -8.0m; // Very heavy penalty for going back to previous position with no benefit
                }
            }

            // Check if we'd go to a position we visited very recently (but not immediately previous)
            if (prevPositions.Count >= 3)
            {
                for (int i = 2; i < Math.Min(5, prevPositions.Count); i++)
                {
                    var recentPos = prevPositions[i];
                    if (nx == recentPos.Item1 && ny == recentPos.Item2)
                    {
                        return -2.0m * (5 - i); // Penalty decreases with age of position
                    }
                }
            }

            return 0m;
        }

        /// <summary>
        /// Rewards consistent movement toward a goal
        /// </summary>
        public static decimal MovementConsistency(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);

            // Check if we're moving onto a pellet (immediate goal achieved)
            bool movingToPellet = state.Cells.Any(c =>
                c.X == nx && c.Y == ny && c.Content == CellContent.Pellet
            );

            if (movingToPellet)
                return 2.0m; // Strong reward for reaching a goal

            // Find closest pellet as potential goal
            var closestPellet = FindClosestPellet(state, me.X, me.Y);
            if (closestPellet == null)
                return 0m;

            // Calculate distance to closest pellet from current and new positions
            int currentDist = FindShortestPathLength(
                state,
                me.X,
                me.Y,
                closestPellet.X,
                closestPellet.Y
            );
            int newDist = FindShortestPathLength(state, nx, ny, closestPellet.X, closestPellet.Y);

            // Strongly reward moves that make progress toward the closest pellet
            if (newDist < currentDist)
                return 1.5m;

            return 0m;
        }

        /// <summary>
        /// Optimizes pellet collection by minimizing empty cell movements
        /// Optimized for performance to stay within 150ms per tick
        /// </summary>
        public static decimal OptimalPelletPathEfficiency(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);

            // If moving directly onto a pellet, that's always optimal
            bool movingToPellet = state.Cells.Any(c =>
                c.X == nx && c.Y == ny && c.Content == CellContent.Pellet
            );

            if (movingToPellet)
                return 3.0m; // Maximum reward

            // Find closest pellet using simple Manhattan distance (much faster than path finding)
            // Get only nearest 5 pellets by Manhattan distance to avoid expensive calculations
            var nearestPellets = state
                .Cells.Where(c => c.Content == CellContent.Pellet)
                .OrderBy(p => Math.Abs(p.X - nx) + Math.Abs(p.Y - ny))
                .Take(3)
                .ToList();

            if (!nearestPellets.Any())
                return 0m;

            // Use the closest pellet by Manhattan distance
            var closestPellet = nearestPellets.First();
            int manhattanDist = Math.Abs(closestPellet.X - nx) + Math.Abs(closestPellet.Y - ny);

            // Calculate simple path efficiency - higher for shorter distances
            decimal pathEfficiency = 3.0m / (manhattanDist + 1);

            // Check if there are additional pellets nearby the target pellet (pellet clustering)
            int pelletClusterCount = nearestPellets.Count(p =>
                Math.Abs(p.X - closestPellet.X) + Math.Abs(p.Y - closestPellet.Y) <= 3
            );

            // Bonus for moves that lead toward pellet clusters
            if (pelletClusterCount > 1)
                pathEfficiency += 0.5m * pelletClusterCount;

            // Check if we're getting closer to the nearest pellet
            int currentManhattanDist =
                Math.Abs(closestPellet.X - me.X) + Math.Abs(closestPellet.Y - me.Y);
            if (manhattanDist < currentManhattanDist)
                pathEfficiency += 1.0m; // Reward for getting closer
            else if (manhattanDist > currentManhattanDist)
                pathEfficiency -= 1.0m; // Penalty for getting farther

            return pathEfficiency;
        }
    }
}
