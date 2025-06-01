using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics; //  >>> NEW (time-budget guard)
using System.Linq; //  >>> NEW (move up for grouping)
using DeepMCTS.Enums;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

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
        var sw = Stopwatch.StartNew();
        decimal score = 0m;

        // 1️⃣  FAST / CORE heuristics  (always evaluated)
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

        score += HeuristicsImpl.TimerAwareness(state, me, move) * WEIGHTS.TimerAwareness;
        score += HeuristicsImpl.PelletEfficiency(state, me, move) * WEIGHTS.PelletEfficiency;
        score += HeuristicsImpl.PositionalDominance(state, me, move) * WEIGHTS.PositionalDominance;
        score += HeuristicsImpl.ScoreLossMinimizer(state, me, move) * WEIGHTS.ScoreLossMinimizer;
        score +=
            HeuristicsImpl.AnticipateCompetition(state, me, move) * WEIGHTS.AnticipateCompetition;

        score += HeuristicsImpl.WallCollisionRisk(state, me, move) * WEIGHTS.WallCollisionRisk; //  >>> NEW
        score += HeuristicsImpl.LineOfSightPellets(state, me, move) * WEIGHTS.LineOfSightPellets; //  >>> NEW
        score += HeuristicsImpl.PelletRace(state, me, move) * WEIGHTS.PelletRace; //  >>> NEW
        score += HeuristicsImpl.RecalcWindowSafety(state, me, move) * WEIGHTS.RecalcWindowSafety; //  >>> NEW
        score += HeuristicsImpl.CenterControl(state, me, move) * WEIGHTS.CenterControl; //  >>> NEW

        // Update path memory
        UpdatePathMemory(state, me);

        // Add anti-cycling measures
        score += HeuristicsImpl.CycleDetection(state, me, move) * WEIGHTS.CycleDetection;
        score += HeuristicsImpl.DirectionalVariety(state, me, move) * WEIGHTS.DirectionalVariety;
        score += HeuristicsImpl.EmptyCellAvoidance(state, me, move) * WEIGHTS.EmptyCellAvoidance;

        // >>> NEW: Heuristics inspired by GatherNear bot for maze navigation
        score += HeuristicsImpl.MoveIfIdle(state, me, move) * WEIGHTS.MoveIfIdle;
        score +=
            HeuristicsImpl.ChangeDirectionWhenStuck(state, me, move)
            * WEIGHTS.ChangeDirectionWhenStuck;
        score += HeuristicsImpl.ShortestPathToGoal(state, me, move) * WEIGHTS.ShortestPathToGoal;
        score += HeuristicsImpl.EdgeAwareness(state, me, move) * WEIGHTS.EdgeAwareness;
        score += HeuristicsImpl.UnoccupiedCellBonus(state, me, move) * WEIGHTS.UnoccupiedCellBonus;
        score +=
            HeuristicsImpl.OpponentTrailChasing(state, me, move) * WEIGHTS.OpponentTrailChasing;
        score += HeuristicsImpl.CenterDistanceBonus(state, me, move) * WEIGHTS.CenterDistanceBonus;
        score += HeuristicsImpl.MovementConsistency(state, me, move) * WEIGHTS.MovementConsistency;
        score += HeuristicsImpl.TunnelNavigation(state, me, move) * WEIGHTS.TunnelNavigation;
        score +=
            HeuristicsImpl.EarlyGameZookeeperAvoidance(state, me, move)
            * WEIGHTS.EarlyGameZookeeperAvoidance;

        // >>> NEW: Territory control inspired heuristics for pellet area domination
        score += HeuristicsImpl.PelletAreaControl(state, me, move) * WEIGHTS.PelletAreaControl;
        score += HeuristicsImpl.DensityMapping(state, me, move) * WEIGHTS.DensityMapping;
        score += HeuristicsImpl.CornerControl(state, me, move) * WEIGHTS.CornerControl;
        score += HeuristicsImpl.AdaptivePathfinding(state, me, move) * WEIGHTS.AdaptivePathfinding;

        return score;
    }

    static class HeuristicsImpl
    {
        //  ————————————————————————— **NEW HEURISTICS** —————————————————————————

        /// <summary>Penalise moves that will crash into a wall within ≤2 tiles,
        /// because that forces us to burn another queued command and lose tempo.</summary>
        public static decimal WallCollisionRisk(GameState st, Animal me, BotAction m) //  >>> NEW
        {
            var (x, y) = ApplyMove(me.X, me.Y, m);
            int steps = 0;
            while (steps < 3 && IsTraversable(st, x, y))
            {
                (x, y) = ApplyMove(x, y, m);
                steps++;
            }
            if (!IsTraversable(st, x, y))
                steps--;
            return steps switch
            {
                0 => -2.0m, // would hit wall next tick
                1 => -0.8m,
                2 => -0.3m,
                _ => 0m,
            };
        }

        /// <summary>Reward straight-line runs that hoover multiple pellets before next turn.</summary>
        public static decimal LineOfSightPellets(GameState st, Animal me, BotAction m) //  >>> NEW
        {
            var (x, y) = ApplyMove(me.X, me.Y, m);
            int visPellets = 0;
            for (int i = 0; i < 6 && IsTraversable(st, x, y); i++)
            {
                if (st.Cells.Any(c => c.X == x && c.Y == y && c.Content == CellContent.Pellet))
                    visPellets++;
                (x, y) = ApplyMove(x, y, m);
            }
            return visPellets * 0.6m;
        }

        /// <summary>Prefer pellets that we can beat _all_ competitors to.</summary>
        public static decimal PelletRace(GameState st, Animal me, BotAction m) //  >>> NEW
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            var pellets = st.Cells.Where(c => c.Content == CellContent.Pellet);
            if (!pellets.Any())
                return 0m;
            var best = pellets.OrderBy(c => ManhattanDistance(nx, ny, c.X, c.Y)).First();
            int myD = ManhattanDistance(nx, ny, best.X, best.Y);
            int minOther = st
                .Animals.Where(a => a.Id != me.Id && a.IsViable)
                .Select(a => ManhattanDistance(a.X, a.Y, best.X, best.Y))
                .DefaultIfEmpty(int.MaxValue)
                .Min();
            return minOther - myD >= 2 ? 1.2m : 0m;
        }

        /// <summary>Avoid being within capture range at the exact tick when the zookeeper retargets
        /// (every 20 ticks per rules §Zookeeper). That is when surprise retargets happen.</summary>
        public static decimal RecalcWindowSafety(GameState st, Animal me, BotAction m) //  >>> NEW
        {
            int ticksToRecalc = (20 - (st.Tick % 20)) % 20;
            if (ticksToRecalc > 3)
                return 0m; // evaluate only in last 3 ticks of window
            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            int dist = st.Zookeepers.Any()
                ? st.Zookeepers.Min(z => ManhattanDistance(z.X, z.Y, nx, ny))
                : 999;
            return dist < 4 ? -1.5m / (dist + 1) : 0.4m; // move away if close, mild bonus if safe
        }

        /// <summary>Because the map is symmetrical, controlling the centre denies pellets to
        /// _all_ quadrants while minimising travel.  We give a small, smooth bonus for staying
        /// within a Manhattan radius ≤ 2 of centre.</summary>
        public static decimal CenterControl(GameState st, Animal me, BotAction m) //  >>> NEW
        {
            int cx = (st.Cells.Min(c => c.X) + st.Cells.Max(c => c.X)) / 2;
            int cy = (st.Cells.Min(c => c.Y) + st.Cells.Max(c => c.Y)) / 2;
            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            int distToCenter = ManhattanDistance(nx, ny, cx, cy);
            return distToCenter <= 2 ? 0.3m : 0m;
        }

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
            var dists = state.Zookeepers.Select(z => ManhattanDistance(z.X, z.Y, nx, ny));
            if (!dists.Any())
                return 0m;
            var minDist = dists.Min();
            return 1m / (minDist + 1);
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
                        .First();

                    // Determine if we're moving toward or away from the closest pellet
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

            return 0m;
        }

        //  ————————————————————————— **GETHERNEAR-INSPIRED HEURISTICS** —————————————————————————

        /// <summary>Strongly encourage movement when idle - based on GatherNear's MoveIfIdle heuristic</summary>
        public static decimal MoveIfIdle(GameState state, Animal me, BotAction move)
        {
            // Since we don't have queue info, just provide a small bonus for any movement
            return move != default(BotAction) ? 1.0m : 0m;
        }

        /// <summary>Change direction when stuck against a wall - based on GatherNear's ChangeDirectionWhenStuck</summary>
        public static decimal ChangeDirectionWhenStuck(GameState state, Animal me, BotAction move)
        {
            var (currentX, currentY) = (me.X, me.Y);

            // Since we don't have action history, we'll use a simple wall detection approach
            // Try each direction and see if any would result in hitting a wall
            var directions = new[]
            {
                BotAction.Up,
                BotAction.Down,
                BotAction.Left,
                BotAction.Right,
            };
            int blockedDirections = 0;

            foreach (var dir in directions)
            {
                var (testX, testY) = ApplyMove(currentX, currentY, dir);
                if (!IsTraversable(state, testX, testY))
                {
                    blockedDirections++;
                }
            }

            // If we're in a tight spot with many blocked directions, reward any valid move
            if (blockedDirections >= 2)
            {
                var (newX, newY) = ApplyMove(currentX, currentY, move);
                return IsTraversable(state, newX, newY) ? 2.0m : -1.0m;
            }

            return 0m;
        }

        /// <summary>Use A* pathfinding to find optimal routes to pellets - inspired by GatherNear's pathfinding</summary>
        public static decimal ShortestPathToGoal(GameState state, Animal me, BotAction move)
        {
            var (newX, newY) = ApplyMove(me.X, me.Y, move);
            var nearestPellet = state
                .Cells.Where(c => c.Content == CellContent.Pellet)
                .OrderBy(c => ManhattanDistance(newX, newY, c.X, c.Y))
                .FirstOrDefault();

            if (nearestPellet == null)
                return 0m;

            // Simple heuristic: prefer moves that reduce Manhattan distance to nearest pellet
            int currentDist = ManhattanDistance(me.X, me.Y, nearestPellet.X, nearestPellet.Y);
            int newDist = ManhattanDistance(newX, newY, nearestPellet.X, nearestPellet.Y);

            if (newDist < currentDist)
            {
                return 2.0m; // Bonus for getting closer to pellet
            }
            else if (newDist > currentDist)
            {
                return -1.0m; // Penalty for moving away
            }

            return 0m;
        }

        /// <summary>Stay aware of map edges and avoid getting trapped - inspired by GatherNear's edge detection</summary>
        public static decimal EdgeAwareness(GameState state, Animal me, BotAction move)
        {
            var (newX, newY) = ApplyMove(me.X, me.Y, move);

            // Calculate distance to nearest edge
            int minX = state.Cells.Min(c => c.X);
            int maxX = state.Cells.Max(c => c.X);
            int minY = state.Cells.Min(c => c.Y);
            int maxY = state.Cells.Max(c => c.Y);

            int distToEdge = Math.Min(
                Math.Min(newX - minX, maxX - newX),
                Math.Min(newY - minY, maxY - newY)
            );

            // Slight penalty for being too close to edges (can limit escape routes)
            if (distToEdge <= 1)
            {
                return -1.5m;
            }
            else if (distToEdge == 2)
            {
                return -0.5m;
            }

            return 0m;
        }

        /// <summary>Bonus for moving towards unoccupied/unexplored areas - inspired by GatherNear's exploration</summary>
        public static decimal UnoccupiedCellBonus(GameState state, Animal me, BotAction move)
        {
            var (newX, newY) = ApplyMove(me.X, me.Y, move);

            // Count how many neighboring cells around the new position are empty
            var directions = new[]
            {
                (0, 1),
                (0, -1),
                (1, 0),
                (-1, 0),
                (1, 1),
                (1, -1),
                (-1, 1),
                (-1, -1),
            };

            int emptyNeighbors = 0;
            foreach (var (dx, dy) in directions)
            {
                int checkX = newX + dx;
                int checkY = newY + dy;

                // Check if this cell is empty (no wall, no other animals)
                if (IsTraversable(state, checkX, checkY))
                {
                    bool hasAnimal = state.Animals.Any(a => a.X == checkX && a.Y == checkY);
                    if (!hasAnimal)
                    {
                        emptyNeighbors++;
                    }
                }
            }

            // Bonus based on how many open spaces are around us (mobility)
            return emptyNeighbors * 0.3m;
        }

        /// <summary>Track and follow opponent movement patterns - inspired by GatherNear's opponent trail chasing</summary>
        public static decimal OpponentTrailChasing(GameState state, Animal me, BotAction move)
        {
            var (newX, newY) = ApplyMove(me.X, me.Y, move);

            // Find the nearest opponent
            var nearestOpponent = state
                .Animals.Where(a => a.Id != me.Id && a.IsViable)
                .OrderBy(a => ManhattanDistance(newX, newY, a.X, a.Y))
                .FirstOrDefault();

            if (nearestOpponent == null)
                return 0m;

            int distToOpponent = ManhattanDistance(
                newX,
                newY,
                nearestOpponent.X,
                nearestOpponent.Y
            );

            // If we're moving closer to an opponent, small bonus (but not too close to avoid capture)
            if (distToOpponent > 3 && distToOpponent < 8)
            {
                int currentDist = ManhattanDistance(
                    me.X,
                    me.Y,
                    nearestOpponent.X,
                    nearestOpponent.Y
                );
                if (distToOpponent < currentDist)
                {
                    return 1.0m; // Small bonus for moving towards opponents at safe distance
                }
            }

            return 0m;
        }

        /// <summary>Bonus for staying reasonably close to map center - inspired by GatherNear's MoveToCenterBonus</summary>
        public static decimal CenterDistanceBonus(GameState state, Animal me, BotAction move)
        {
            int centerX = (state.Cells.Min(c => c.X) + state.Cells.Max(c => c.X)) / 2;
            int centerY = (state.Cells.Min(c => c.Y) + state.Cells.Max(c => c.Y)) / 2;

            var (newX, newY) = ApplyMove(me.X, me.Y, move);
            int currentDist = ManhattanDistance(me.X, me.Y, centerX, centerY);
            int newDist = ManhattanDistance(newX, newY, centerX, centerY);

            // Small bonus for moving towards center, penalty for moving too far away
            if (newDist < currentDist && newDist > 2) // Don't cluster exactly at center
            {
                return 0.8m;
            }
            else if (newDist > 15) // Penalty for being too far from center
            {
                return -0.4m;
            }

            return 0m;
        }

        /// <summary>Encourage consistent movement direction to avoid oscillation - inspired by GatherNear's consistency</summary>
        public static decimal MovementConsistency(GameState state, Animal me, BotAction move)
        {
            // Without action history, we'll use a simple heuristic based on position
            // Favor moves that continue in a straight line when possible

            // Use the recent positions to infer direction (if available through static tracking)
            string animalKey = me.Id.ToString();
            if (_recentPositions.ContainsKey(animalKey))
            {
                var positions = _recentPositions[animalKey];
                if (positions.Count >= 2)
                {
                    var recentPos = positions.ToArray();
                    var lastPos = recentPos[recentPos.Length - 1];
                    var secondLastPos = recentPos[recentPos.Length - 2];

                    // Infer the recent direction
                    int deltaX = lastPos.Item1 - secondLastPos.Item1;
                    int deltaY = lastPos.Item2 - secondLastPos.Item2;

                    BotAction inferredDirection = BotAction.Up;
                    if (deltaX > 0)
                        inferredDirection = BotAction.Right;
                    else if (deltaX < 0)
                        inferredDirection = BotAction.Left;
                    else if (deltaY > 0)
                        inferredDirection = BotAction.Down;
                    else if (deltaY < 0)
                        inferredDirection = BotAction.Up;

                    // Bonus for continuing in the same direction
                    if (move == inferredDirection)
                    {
                        return 0.6m;
                    }

                    // Penalty for reversing direction
                    var opposites = new Dictionary<BotAction, BotAction>
                    {
                        { BotAction.Up, BotAction.Down },
                        { BotAction.Down, BotAction.Up },
                        { BotAction.Left, BotAction.Right },
                        { BotAction.Right, BotAction.Left },
                    };

                    if (
                        opposites.ContainsKey(inferredDirection)
                        && opposites[inferredDirection] == move
                    )
                    {
                        return -1.2m;
                    }
                }
            }

            return 0m;
        }

        /// <summary>Navigate through tight spaces and tunnels effectively - inspired by maze navigation</summary>
        public static decimal TunnelNavigation(GameState state, Animal me, BotAction move)
        {
            var (newX, newY) = ApplyMove(me.X, me.Y, move);

            // Count traversable neighbors around new position
            var directions = new[] { (0, 1), (0, -1), (1, 0), (-1, 0) };
            int traversableNeighbors = 0;

            foreach (var (dx, dy) in directions)
            {
                if (IsTraversable(state, newX + dx, newY + dy))
                {
                    traversableNeighbors++;
                }
            }

            // If we're in a tunnel (only 2 directions available), encourage continuing
            if (traversableNeighbors == 2)
            {
                return 1.0m; // Bonus for tunnel navigation
            }

            // If we have many options, we're in an open area
            if (traversableNeighbors >= 3)
            {
                return 0.4m; // Small bonus for being in open areas with options
            }

            return 0m;
        }

        /// <summary>Move away from zookeeper at game start when it's in the center - critical early survival</summary>
        public static decimal EarlyGameZookeeperAvoidance(
            GameState state,
            Animal me,
            BotAction move
        )
        {
            // Only apply this heuristic in the first 10 ticks of the game
            if (state.Tick > 10)
                return 0m;

            // Calculate map center (where zookeeper starts)
            int centerX = (state.Cells.Min(c => c.X) + state.Cells.Max(c => c.X)) / 2;
            int centerY = (state.Cells.Min(c => c.Y) + state.Cells.Max(c => c.Y)) / 2;

            var (newX, newY) = ApplyMove(me.X, me.Y, move);

            // Calculate current and new distances from center
            int currentDistFromCenter = ManhattanDistance(me.X, me.Y, centerX, centerY);
            int newDistFromCenter = ManhattanDistance(newX, newY, centerX, centerY);

            // Strong bonus for moving away from center, strong penalty for moving toward it
            if (newDistFromCenter > currentDistFromCenter)
            {
                return 5.0m; // Strong bonus for moving away from center at start
            }
            else if (newDistFromCenter < currentDistFromCenter)
            {
                return -3.0m; // Strong penalty for moving toward center at start
            }

            // Additional bonus if we're still very close to center (emergency escape)
            if (currentDistFromCenter <= 3)
            {
                return 2.0m; // Extra urgency to get away
            }

            return 0m;
        }

        // >>> NEW: Territory control inspired heuristics for pellet area domination

        /// <summary>Control key areas that have high pellet density - inspired by territory control</summary>
        public static decimal PelletAreaControl(GameState state, Animal me, BotAction move)
        {
            var (newX, newY) = ApplyMove(me.X, me.Y, move);

            // Find areas with highest pellet concentration within a radius
            int radius = 4;
            int pelletsInArea = 0;

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int checkX = newX + dx;
                    int checkY = newY + dy;

                    if (
                        state.Cells.Any(c =>
                            c.X == checkX && c.Y == checkY && c.Content == CellContent.Pellet
                        )
                    )
                    {
                        pelletsInArea++;
                    }
                }
            }

            // Bonus for moving into areas with many pellets nearby
            return pelletsInArea * 0.2m;
        }

        /// <summary>Map and prioritize high-density pellet areas - like territory value calculation</summary>
        public static decimal DensityMapping(GameState state, Animal me, BotAction move)
        {
            var (newX, newY) = ApplyMove(me.X, me.Y, move);

            // Calculate local pellet density
            var nearbyPellets = state
                .Cells.Where(c => c.Content == CellContent.Pellet)
                .Where(c => ManhattanDistance(newX, newY, c.X, c.Y) <= 3)
                .ToList();

            if (!nearbyPellets.Any())
                return 0m;

            // Calculate density (pellets per unit area)
            decimal density = nearbyPellets.Count / 9.0m; // 3x3 area

            return density * 1.5m;
        }

        /// <summary>Control map corners for strategic advantage - inspired by GatherNear's corner strategies</summary>
        public static decimal CornerControl(GameState state, Animal me, BotAction move)
        {
            var (newX, newY) = ApplyMove(me.X, me.Y, move);

            // Get map boundaries
            int minX = state.Cells.Min(c => c.X);
            int maxX = state.Cells.Max(c => c.X);
            int minY = state.Cells.Min(c => c.Y);
            int maxY = state.Cells.Max(c => c.Y);

            // Check if we're moving towards a corner
            bool nearCorner =
                (newX <= minX + 2 || newX >= maxX - 2) && (newY <= minY + 2 || newY >= maxY - 2);

            if (nearCorner)
            {
                // Count pellets in corner areas
                int cornerPellets = state
                    .Cells.Where(c => c.Content == CellContent.Pellet)
                    .Where(c =>
                        (c.X <= minX + 3 || c.X >= maxX - 3) && (c.Y <= minY + 3 || c.Y >= maxY - 3)
                    )
                    .Count();

                return cornerPellets > 0 ? 1.5m : 0.3m; // Bonus if corner has pellets
            }

            return 0m;
        }

        /// <summary>Adapt pathfinding based on game state - inspired by dynamic strategy adjustment</summary>
        public static decimal AdaptivePathfinding(GameState state, Animal me, BotAction move)
        {
            var (newX, newY) = ApplyMove(me.X, me.Y, move);

            // Adapt strategy based on game progress
            int totalPellets = state.Cells.Count(c => c.Content == CellContent.Pellet);
            int viableOpponents = state.Animals.Count(a => a.Id != me.Id && a.IsViable);

            // Early game: focus on exploration
            if (totalPellets > 100)
            {
                // Bonus for moving to unexplored areas
                bool isUnexplored = !state.Animals.Any(a =>
                    ManhattanDistance(a.X, a.Y, newX, newY) < 3
                );
                return isUnexplored ? 1.0m : 0m;
            }
            // Mid game: balanced approach
            else if (totalPellets > 30)
            {
                // Balance between efficiency and safety
                var nearestPellet = state
                    .Cells.Where(c => c.Content == CellContent.Pellet)
                    .OrderBy(c => ManhattanDistance(newX, newY, c.X, c.Y))
                    .FirstOrDefault();

                if (nearestPellet != null)
                {
                    int distToPellet = ManhattanDistance(
                        newX,
                        newY,
                        nearestPellet.X,
                        nearestPellet.Y
                    );
                    return distToPellet <= 2 ? 0.8m : 0m;
                }
            }
            // Endgame: aggressive pellet collection
            else
            {
                // Prioritize getting the last pellets
                var nearestPellet = state
                    .Cells.Where(c => c.Content == CellContent.Pellet)
                    .OrderBy(c => ManhattanDistance(newX, newY, c.X, c.Y))
                    .FirstOrDefault();

                if (nearestPellet != null)
                {
                    int currentDist = ManhattanDistance(
                        me.X,
                        me.Y,
                        nearestPellet.X,
                        nearestPellet.Y
                    );
                    int newDist = ManhattanDistance(newX, newY, nearestPellet.X, nearestPellet.Y);
                    return newDist < currentDist ? 2.0m : -0.5m;
                }
            }

            return 0m;
        }
    }
}
