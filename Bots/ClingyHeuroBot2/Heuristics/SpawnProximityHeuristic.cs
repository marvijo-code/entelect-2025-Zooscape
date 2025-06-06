#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class SpawnProximityHeuristic : IHeuristic
{
    public string Name => "SpawnProximity";

    public decimal CalculateRawScore(IHeuristicContext heuristicContext)
    {
        var (nx, ny) = heuristicContext.MyNewPosition;

        int spawnDist = Heuristics.ManhattanDistance(
            heuristicContext.CurrentAnimal.SpawnX,
            heuristicContext.CurrentAnimal.SpawnY,
            nx,
            ny
        );

        if (spawnDist < 3 && heuristicContext.CurrentGameState.Tick < 50)
        {
            return -1.0m * (3 - spawnDist);
        }

        return (decimal)spawnDist / 10.0m;
    }
}
