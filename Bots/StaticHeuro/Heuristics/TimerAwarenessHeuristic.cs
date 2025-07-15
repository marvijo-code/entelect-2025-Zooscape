using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class TimerAwarenessHeuristic : IHeuristic
{
    public string Name => "TimerAwareness";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var tick = heuristicContext.CurrentGameState.Tick;
        var weight = heuristicContext.Weights.TimerAwareness;

        // Scale tick to thousands to keep numbers small
        return weight * ((decimal)tick / 1000m);
    }
}
