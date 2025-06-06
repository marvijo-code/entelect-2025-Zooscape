#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class DistanceToGoalHeuristic : IHeuristic
{
    public string Name => "DistanceToGoal";

    public decimal CalculateRawScore(IHeuristicContext heuristicContext)
    {
        var (nx, ny) = Heuristics.ApplyMove(
            heuristicContext.CurrentAnimal.X,
            heuristicContext.CurrentAnimal.Y,
            heuristicContext.CurrentMove
        );
        var pellets = heuristicContext.CurrentGameState.Cells.Where(c =>
            c.Content == CellContent.Pellet
        );
        if (!pellets.Any())
        {
            heuristicContext.Logger?.Verbose("{Heuristic}: No pellets found.", Name);
            return 0m;
        }

        // Get the closest pellet
        var closestPellet = pellets.OrderBy(p => Math.Abs(p.X - nx) + Math.Abs(p.Y - ny)).First();

        var distance = Math.Abs(closestPellet.X - nx) + Math.Abs(closestPellet.Y - ny);

        // Heavily reward moves that get us closer to the nearest pellet
        if (
            distance
            < Math.Abs(closestPellet.X - heuristicContext.CurrentAnimal.X)
                + Math.Abs(closestPellet.Y - heuristicContext.CurrentAnimal.Y)
        )
            return 2.0m;
        else if (
            distance
            > Math.Abs(closestPellet.X - heuristicContext.CurrentAnimal.X)
                + Math.Abs(closestPellet.Y - heuristicContext.CurrentAnimal.Y)
        )
            return -1.0m;

        // If we're not making progress toward the closest pellet, consider the overall pellet situation
        // Original logic: return -minDist * 0.5m;
        // The intention is to penalize being far from pellets. A smaller minDist is better.
        // So, a larger negative score (more penalty) if minDist is large.
        var minDistToAnyPellet = pellets.Min(c => Heuristics.ManhattanDistance(c.X, c.Y, nx, ny));
        return -(decimal)minDistToAnyPellet * 0.5m;
    }
}
