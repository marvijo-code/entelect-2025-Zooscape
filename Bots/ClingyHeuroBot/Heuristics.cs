using System;
using System.Collections.Generic;
using System.Linq;
using HeuroBot.Enums;
using HeuroBot.Models;

namespace HeuroBot.Services;

public static class Heuristics
{
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
            var minDist = pellets.Min(c => ManhattanDistance(c.X, c.Y, nx, ny));
            return -minDist;
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
            return state.Cells.Count(c =>
                c.Content == CellContent.Pellet && ManhattanDistance(c.X, c.Y, nx, ny) <= 2
            );
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

            return edgeDistance <= 1 ? -0.3m : 0m;
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
    }
}
