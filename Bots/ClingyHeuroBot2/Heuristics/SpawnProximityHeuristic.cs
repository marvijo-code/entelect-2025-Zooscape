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

        return (decimal)spawnDist / heuristicContext.Weights.SpawnProximityDistanceBonusDivisor;
    }
}
