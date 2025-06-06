// #pragma warning disable SKEXP0110 // Disable SK Experimental warning
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class ZookeeperCooldownHeuristic : IHeuristic
{
    public string Name => "ZookeeperCooldown";

    public decimal CalculateRawScore(IHeuristicContext heuristicContext)
    {
        heuristicContext.Logger?.Verbose("{Heuristic} not implemented", Name);
        return 0m;
    }
}
