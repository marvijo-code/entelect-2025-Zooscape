using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace StaticHeuro.Heuristics;

public class TravelDistanceMaximizerHeuristic : IHeuristic
{
    public string Name => "TravelDistanceMaximizer";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        heuristicContext.Logger?.Verbose("{Heuristic} not implemented", Name);
        return 0m;
    }
}
