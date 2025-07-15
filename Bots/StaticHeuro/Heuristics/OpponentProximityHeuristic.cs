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

        var opponents = heuristicContext.CurrentGameState.Animals
            .Where(a => a.Id != heuristicContext.CurrentAnimal.Id && a.IsViable)
            .ToList();

        if (!opponents.Any())
            return 0m;

        var dists = opponents.Select(o => 
            BotUtils.ManhattanDistance(o.X, o.Y, nx, ny)
        );

        if (!dists.Any())
            return 0m;

        var minDist = dists.Min();

        if (minDist < 0) return 0m;

        // Hard override for critical proximity
        if (minDist <= 3) 
        {
            return -10000.0m; // Large penalty to veto unsafe moves
        }

        return -heuristicContext.Weights.OpponentProximity / (minDist + 1.0m);
    }
}
