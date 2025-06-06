#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics
{
    public class DistanceToGoalHeuristic : IHeuristic
    {
        public string Name => "DistanceToGoal";

        public decimal CalculateRawScore(
            GameState state,
            Animal me,
            BotAction move,
            ILogger? logger
        )
        {
            var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);
            var pellets = state.Cells.Where(c => c.Content == CellContent.Pellet);
            if (!pellets.Any())
                return 0m;

            // Get the closest pellet
            var closestPellet = pellets
                .OrderBy(c => Heuristics.ManhattanDistance(c.X, c.Y, nx, ny))
                .FirstOrDefault();
            if (closestPellet == null) // Should not happen if pellets.Any() is true, but good for safety
                return 0m;

            int currentDist = Heuristics.ManhattanDistance(
                me.X,
                me.Y,
                closestPellet.X,
                closestPellet.Y
            );
            int newDist = Heuristics.ManhattanDistance(nx, ny, closestPellet.X, closestPellet.Y);

            // Heavily reward moves that get us closer to the nearest pellet
            if (newDist < currentDist)
                return 2.0m;
            else if (newDist > currentDist)
                return -1.0m;

            // If we're not making progress toward the closest pellet, consider the overall pellet situation
            // Original logic: return -minDist * 0.5m;
            // The intention is to penalize being far from pellets. A smaller minDist is better.
            // So, a larger negative score (more penalty) if minDist is large.
            var minDistToAnyPellet = pellets.Min(c =>
                Heuristics.ManhattanDistance(c.X, c.Y, nx, ny)
            );
            return -(decimal)minDistToAnyPellet * 0.5m;
        }
    }
}
