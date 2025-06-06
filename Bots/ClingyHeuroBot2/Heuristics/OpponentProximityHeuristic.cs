#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using ClingyHeuroBot2;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class OpponentProximityHeuristic : IHeuristic
{
    public string Name => "OpponentProximity";

    public decimal CalculateRawScore(IHeuristicContext heuristicContext)
    {
        var (nx, ny) = heuristicContext.MyNewPosition;

        if (state.Zookeepers == null || !state.Zookeepers.Any())
            return 0m;

        var dists = state.Zookeepers.Select(z => Heuristics.ManhattanDistance(z.X, z.Y, nx, ny));

        if (!dists.Any())
            return 0m;

        var minDist = dists.Min();

        if (minDist < 0)
            return 0m;

        return 1m / (minDist + 1.0m);
    }
}
