using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace StaticHeuro.Heuristics;

public class PelletAreaControlHeuristic : IHeuristic
{
    public string Name => "PelletAreaControl";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        return 0m;
    }
}
