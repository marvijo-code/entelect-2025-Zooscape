using System.Linq;
using ClingyHeuroBot2; // Added for IHeuristic
using DeepMCTS.Enums; // Corrected namespace for CellContent
using Marvijo.Zooscape.Bots.Common.Enums; // Added for BotAction
using Marvijo.Zooscape.Bots.Common.Models;

namespace HeuroBot.Bots.ClingyHeuroBot2.Heuristics
{
    public class DistanceToGoalHeuristic : IHeuristic
    {
        public string Name => "DistanceToGoal";

        public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
        {
            var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);
            var pellets = state.Cells.Where(c => c.Content == DeepMCTS.Enums.CellContent.Pellet);
            if (!pellets.Any())
                return 0m;

            var closestPellet = pellets
                .OrderBy(c => Heuristics.ManhattanDistance(c.X, c.Y, nx, ny))
                .FirstOrDefault();
            if (closestPellet == null)
                return 0m;

            int currentDist = Heuristics.ManhattanDistance(
                me.X,
                me.Y,
                closestPellet.X,
                closestPellet.Y
            );
            int newDist = Heuristics.ManhattanDistance(nx, ny, closestPellet.X, closestPellet.Y);

            if (newDist < currentDist)
                return 2.0m; // Closer to the nearest pellet
            else if (newDist > currentDist)
                return -1.0m; // Further from the nearest pellet

            // If not making progress toward the closest pellet,
            // consider the overall minimum distance to any pellet from the new position.
            // This encourages moving towards general pellet areas if the absolute closest isn't better.
            var minDistToAnyPellet = pellets.Min(c =>
                Heuristics.ManhattanDistance(c.X, c.Y, nx, ny)
            );
            return -minDistToAnyPellet * 0.5m; // Penalize based on distance, less is better
        }
    }
}
