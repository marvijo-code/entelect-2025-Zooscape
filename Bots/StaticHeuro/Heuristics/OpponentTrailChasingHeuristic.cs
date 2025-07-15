#pragma warning disable SKEXP0110
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class OpponentTrailChasingHeuristic : IHeuristic
{
    public string Name => "OpponentTrailChasing";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var (nx, ny) = heuristicContext.MyNewPosition;

        // Find the nearest opponent
        var nearestOpponent = heuristicContext
            .CurrentGameState.Animals.Where(a => a.Id != heuristicContext.CurrentAnimal.Id && a.IsViable) // Consider only viable opponents
            .OrderBy(a => BotUtils.ManhattanDistance(nx, ny, a.X, a.Y))
            .FirstOrDefault();

        if (nearestOpponent == null)
            return 0m; // No opponents, no score

        int distToOpponent = BotUtils.ManhattanDistance(
            nx,
            ny,
            nearestOpponent.X,
            nearestOpponent.Y
        );

        // If we're moving closer to an opponent, small bonus (but not too close to avoid capture)
        // Original logic: if (distToOpponent > 3 && distToOpponent < 8)
        // This encourages getting somewhat close to other animals.
        if (distToOpponent > heuristicContext.Weights.OpponentChaseMinDistance && distToOpponent < heuristicContext.Weights.OpponentChaseMaxDistance)
        {
            int currentDist = BotUtils.ManhattanDistance(
                heuristicContext.CurrentAnimal.X,
                heuristicContext.CurrentAnimal.Y,
                nearestOpponent.X,
                nearestOpponent.Y
            );
            if (distToOpponent < currentDist)
            {
                heuristicContext.Logger?.Verbose(
                    "{Heuristic}: Moving towards opponent {OpponentId} at safe distance. Old: {CurrentDist}, New: {NewDist}",
                    Name,
                    nearestOpponent.Id,
                    currentDist,
                    distToOpponent
                );
                return heuristicContext.Weights.OpponentChaseBonus; // Small bonus for moving towards opponents at a safe distance
            }
        }

        return 0m;
    }
}
