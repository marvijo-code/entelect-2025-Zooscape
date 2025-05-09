using System;
using System.Linq;
using HeuroBot.Enums;
using HeuroBot.Models;

namespace HeuroBot.Services;

public static class Heuristics
{
    public static decimal ScoreMove(GameState state, Animal me, BotAction move)
    {
        decimal score = 0m;
        score += HeuristicsImpl.DistanceToGoal(state, me, move) * 1.0m;
        score += HeuristicsImpl.OpponentProximity(state, me, move) * 1.0m;
        score += HeuristicsImpl.ResourceClustering(state, me, move) * 1.0m;
        score += HeuristicsImpl.AreaControl(state, me, move) * 1.0m;
        score += HeuristicsImpl.Mobility(state, me, move) * 1.0m;
        score += HeuristicsImpl.PathSafety(state, me, move) * 1.0m;
        score += HeuristicsImpl.ImmediatePelletBonus(state, me, move) * 2.0m;
        score += HeuristicsImpl.AdjacentPelletCount(state, me, move) * 1.5m;
        score += HeuristicsImpl.PelletsWithinTwoMoves(state, me, move) * 1.0m;
        score += HeuristicsImpl.CompetitivePelletAccess(state, me, move) * 1.2m;
        score += HeuristicsImpl.DirectionalAlignmentBonus(state, me, move) * 0.7m;
        score += HeuristicsImpl.SpawnProximityPenalty(state, me, move) * -1.0m;
        score += HeuristicsImpl.OpponentSpacingBonus(state, me, move) * 0.5m;
        score += HeuristicsImpl.ExtendedResourceClustering(state, me, move) * 0.4m;
        score += HeuristicsImpl.WeightedPelletProximity(state, me, move) * 0.8m;
        score += HeuristicsImpl.CorridorAccessibilityPenalty(state, me, move) * -1.2m;
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

        public static decimal DistanceToGoal(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            var pellets = state.Cells.Where(c => c.Content == CellContent.Pellet);
            if (!pellets.Any())
                return 0m;
            var minDist = pellets.Min(c => Math.Abs(c.X - nx) + Math.Abs(c.Y - ny));
            return -minDist;
        }

        public static decimal OpponentProximity(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            var dists = state.Zookeepers.Select(z => Math.Abs(z.X - nx) + Math.Abs(z.Y - ny));
            if (!dists.Any())
                return 0m;
            var minDist = dists.Min();
            return 1m / (minDist + 1);
        }

        public static decimal ResourceClustering(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            return state.Cells.Count(c =>
                c.Content == CellContent.Pellet && Math.Abs(c.X - nx) + Math.Abs(c.Y - ny) <= 2
            );
        }

        public static decimal AreaControl(GameState state, Animal me, BotAction m) =>
            ResourceClustering(state, me, m);

        public static decimal Mobility(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            return Enum.GetValues<BotAction>()
                .Cast<BotAction>()
                .Count(a =>
                {
                    var (x2, y2) = ApplyMove(nx, ny, a);
                    var cell = state.Cells.FirstOrDefault(c => c.X == x2 && c.Y == y2);
                    return cell != null && cell.Content != CellContent.Wall;
                });
        }

        public static decimal PathSafety(GameState state, Animal me, BotAction m) =>
            Mobility(state, me, m) <= 1 ? 1m : 0m;

        public static decimal ImmediatePelletBonus(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            return state.Cells.Any(c => c.X == nx && c.Y == ny && c.Content == CellContent.Pellet)
                ? 1m
                : 0m;
        }

        public static decimal AdjacentPelletCount(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            var dirs = new[] { (0, 1), (0, -1), (1, 0), (-1, 0) };
            return dirs.Count(d =>
                state.Cells.Any(c =>
                    c.X == nx + d.Item1 && c.Y == ny + d.Item2 && c.Content == CellContent.Pellet
                )
            );
        }

        public static decimal PelletsWithinTwoMoves(GameState state, Animal me, BotAction m)
        {
            var start = ApplyMove(me.X, me.Y, m);
            var positions = new HashSet<(int x, int y)> { start };
            var queue = new Queue<((int x, int y) pos, int depth)>();
            queue.Enqueue((start, 0));
            while (queue.Any())
            {
                var (pos, depth) = queue.Dequeue();
                if (depth >= 2)
                    continue;
                foreach (BotAction a in Enum.GetValues<BotAction>())
                {
                    var next = ApplyMove(pos.x, pos.y, a);
                    if (!positions.Contains(next))
                    {
                        positions.Add(next);
                        queue.Enqueue((next, depth + 1));
                    }
                }
            }
            return positions.Count(p =>
                state.Cells.Any(c => c.X == p.x && c.Y == p.y && c.Content == CellContent.Pellet)
            );
        }

        public static decimal CompetitivePelletAccess(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            var pellets = state.Cells.Where(c => c.Content == CellContent.Pellet);
            int count = 0;
            foreach (var p in pellets)
            {
                var myDist = Math.Abs(p.X - nx) + Math.Abs(p.Y - ny);
                var otherMin = state
                    .Animals.Where(a => a.Id != me.Id)
                    .Select(a => Math.Abs(a.X - p.X) + Math.Abs(a.Y - p.Y))
                    .DefaultIfEmpty(int.MaxValue)
                    .Min();
                if (myDist < otherMin)
                    count++;
            }
            return count;
        }

        public static decimal DirectionalAlignmentBonus(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            var pellets = state.Cells.Where(c => c.Content == CellContent.Pellet);
            if (!pellets.Any())
                return 0m;
            var nearest = pellets.OrderBy(c => Math.Abs(c.X - me.X) + Math.Abs(c.Y - me.Y)).First();
            return (
                m switch
                {
                    BotAction.Up => nearest.Y < me.Y && nearest.X == me.X,
                    BotAction.Down => nearest.Y > me.Y && nearest.X == me.X,
                    BotAction.Left => nearest.X < me.X && nearest.Y == me.Y,
                    BotAction.Right => nearest.X > me.X && nearest.Y == me.Y,
                    _ => false,
                }
            )
                ? 1m
                : 0m;
        }

        public static decimal SpawnProximityPenalty(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            var dist = Math.Abs(nx - me.SpawnX) + Math.Abs(ny - me.SpawnY);
            return dist == 0 ? 1m
                : dist == 1 ? 0.5m
                : 0m;
        }

        public static decimal OpponentSpacingBonus(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            var others = state.Animals.Where(a => a.Id != me.Id);
            if (!others.Any())
                return 0m;
            var min = others.Select(a => Math.Abs(a.X - nx) + Math.Abs(a.Y - ny)).Min();
            return min;
        }

        public static decimal ExtendedResourceClustering(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            return state.Cells.Count(c =>
                c.Content == CellContent.Pellet && Math.Abs(c.X - nx) + Math.Abs(c.Y - ny) <= 3
            );
        }

        public static decimal WeightedPelletProximity(GameState state, Animal me, BotAction m)
        {
            var (nx, ny) = ApplyMove(me.X, me.Y, m);
            return state
                .Cells.Where(c => c.Content == CellContent.Pellet)
                .Sum(c => 1m / (Math.Abs(c.X - nx) + Math.Abs(c.Y - ny) + 1));
        }

        public static decimal CorridorAccessibilityPenalty(GameState state, Animal me, BotAction m)
        {
            var start = ApplyMove(me.X, me.Y, m);
            var visited = new HashSet<(int x, int y)> { start };
            var queue = new Queue<(int x, int y)>();
            queue.Enqueue(start);
            while (queue.Any() && visited.Count <= 6)
            {
                var (x, y) = queue.Dequeue();
                foreach (BotAction a in Enum.GetValues<BotAction>())
                {
                    var next = ApplyMove(x, y, a);
                    if (
                        !visited.Contains(next)
                        && state.Cells.Any(c =>
                            c.X == next.x && c.Y == next.y && c.Content != CellContent.Wall
                        )
                    )
                    {
                        visited.Add(next);
                        queue.Enqueue(next);
                    }
                }
            }
            return visited.Count <= 6 ? 1m : 0m;
        }
    }
}
