#pragma warning disable SKEXP0110
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class DirectionalVarietyHeuristic : IHeuristic
{
    public string Name => "DirectionalVariety";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var prev = heuristicContext.AnimalLastDirection;
        var current = heuristicContext.CurrentMove;
        var weight = heuristicContext.Weights.DirectionalVariety;

        if (prev == null)
            return 0m; // no info

        return prev == current ? -weight : weight;
    }
}
