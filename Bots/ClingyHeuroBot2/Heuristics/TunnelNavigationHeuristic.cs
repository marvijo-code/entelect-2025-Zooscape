using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class TunnelNavigationHeuristic : IHeuristic
{
    public string Name => "TunnelNavigation";

    public decimal CalculateRawScore(IHeuristicContext heuristicContext)
    {
        logger?.Verbose("{Heuristic} not implemented", Name);
        return 0m;
    }
}
