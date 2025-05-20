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
        score += HeuristicsImpl.DistanceToGoal(state, me, move) * WEIGHTS.DistanceToGoal;
        score += HeuristicsImpl.OpponentProximity(state, me, move) * WEIGHTS.OpponentProximity;
        score += HeuristicsImpl.ResourceClustering(state, me, move) * WEIGHTS.ResourceClustering;
        score += HeuristicsImpl.AreaControl(state, me, move) * WEIGHTS.AreaControl;
        score += HeuristicsImpl.Mobility(state, me, move) * WEIGHTS.Mobility;
        score += HeuristicsImpl.PathSafety(state, me, move) * WEIGHTS.PathSafety;
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
    }
}
