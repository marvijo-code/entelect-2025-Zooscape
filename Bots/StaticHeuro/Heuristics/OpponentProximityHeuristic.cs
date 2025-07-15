#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Utils;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace StaticHeuro.Heuristics;

public class OpponentProximityHeuristic : IHeuristic
{
    public string Name => "OpponentProximity";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var (nx, ny) = heuristicContext.MyNewPosition;

        if (
            heuristicContext.CurrentGameState.Zookeepers == null
            || !heuristicContext.CurrentGameState.Zookeepers.Any()
        )
            return 0m;

        var dists = heuristicContext.CurrentGameState.Zookeepers.Select(z =>
            BotUtils.ManhattanDistance(z.X, z.Y, nx, ny)
        );

        if (!dists.Any())
            return 0m;

        var minDist = dists.Min();

        if (minDist < 0)
            return 0m;

        return -heuristicContext.Weights.OpponentProximity / (minDist + 1.0m);
    }
}
