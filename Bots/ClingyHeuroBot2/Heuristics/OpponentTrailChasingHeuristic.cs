#pragma warning disable SKEXP0110
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class OpponentTrailChasingHeuristic : IHeuristic
{
    public string Name => "OpponentTrailChasing";

    public decimal CalculateRawScore(IHeuristicContext context)
    {
        var (nx, ny) = context.MyNewPosition;

        // Find the nearest opponent
        var nearestOpponent = context
            .CurrentGameState.Animals.Where(a => a.Id != context.CurrentAnimal.Id && a.IsViable) // Consider only viable opponents
            .OrderBy(a => Heuristics.ManhattanDistance(nx, ny, a.X, a.Y))
            .FirstOrDefault();

        if (nearestOpponent == null)
            return 0m; // No opponents, no score

        int distToOpponent = Heuristics.ManhattanDistance(
            nx,
            ny,
            nearestOpponent.X,
            nearestOpponent.Y
        );

        // If we're moving closer to an opponent, small bonus (but not too close to avoid capture)
        // Original logic: if (distToOpponent > 3 && distToOpponent < 8)
        // This encourages getting somewhat close to other animals.
        if (distToOpponent > 3 && distToOpponent < 8)
        {
            int currentDist = Heuristics.ManhattanDistance(
                context.CurrentAnimal.X,
                context.CurrentAnimal.Y,
                nearestOpponent.X,
                nearestOpponent.Y
            );
            if (distToOpponent < currentDist)
            {
                context.Logger?.Verbose(
                    "{Heuristic}: Moving towards opponent {OpponentId} at safe distance. Old: {CurrentDist}, New: {NewDist}",
                    Name,
                    nearestOpponent.Id,
                    currentDist,
                    distToOpponent
                );
                return 1.0m; // Small bonus for moving towards opponents at a safe distance
            }
        }

        return 0m;
    }
}
