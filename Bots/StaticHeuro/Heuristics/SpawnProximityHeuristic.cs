#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class SpawnProximityHeuristic : IHeuristic
{
    public string Name => "SpawnProximity";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var (nx, ny) = heuristicContext.MyNewPosition;

        int spawnDist = BotUtils.ManhattanDistance(
            heuristicContext.CurrentAnimal.SpawnX,
            heuristicContext.CurrentAnimal.SpawnY,
            nx,
            ny
        );

        if (spawnDist < heuristicContext.Weights.SpawnProximityEarlyGameDistanceThreshold && heuristicContext.CurrentGameState.Tick < heuristicContext.Weights.SpawnProximityEarlyGameTickThreshold)
        {
            return heuristicContext.Weights.SpawnProximityEarlyGamePenalty * (heuristicContext.Weights.SpawnProximityEarlyGameDistanceThreshold - spawnDist);
        }

        if (heuristicContext.Weights.SpawnProximityDistanceBonusDivisor == 0m)
        {
            // Prevent division by zero if the divisor is not set or misconfigured.
            // Returning 0 score in this case, as a zero divisor implies an issue with weighting.
            heuristicContext.Logger?.Debug("{HeuristicName}: SpawnProximityDistanceBonusDivisor is zero. Returning 0 score.", Name);
            return 0m;
        }
        return (decimal)spawnDist / heuristicContext.Weights.SpawnProximityDistanceBonusDivisor;
    }
}
