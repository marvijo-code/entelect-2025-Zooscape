#pragma warning disable SKEXP0110
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class AdaptivePathfindingHeuristic : IHeuristic
{
    public string Name => "AdaptivePathfinding";

    public decimal CalculateScore(IHeuristicContext context)
    {
        context.Logger?.Verbose("{Heuristic} not implemented", Name);
        return 0m;
    }
}
