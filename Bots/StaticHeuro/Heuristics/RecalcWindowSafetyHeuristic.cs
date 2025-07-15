#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils;
using Serilog;

namespace StaticHeuro.Heuristics;

public class RecalcWindowSafetyHeuristic : IHeuristic
{
    public string Name => "RecalcWindowSafety";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var weights = heuristicContext.Weights;

        if (weights.RecalcWindowSize <= 0)
        {
            return 0m; // Cannot calculate if window size is zero or negative, so this heuristic has no effect.
        }
        int ticksToRecalc = (weights.RecalcWindowSize - (heuristicContext.CurrentGameState.Tick % weights.RecalcWindowSize)) % weights.RecalcWindowSize;
        if (ticksToRecalc > weights.RecalcWindowSafetyTickThreshold)
            return 0m; // evaluate only in last few ticks of window

        var (nx, ny) = heuristicContext.MyNewPosition;
        int dist = heuristicContext.CurrentGameState.Zookeepers.Any()
            ? heuristicContext.CurrentGameState.Zookeepers.Min(z =>
                BotUtils.ManhattanDistance(z.X, z.Y, nx, ny)
            )
            : 999;

        return dist < weights.RecalcWindowSafetyDistanceThreshold 
            ? weights.RecalcWindowSafetyPenalty / (dist + 1) 
            : weights.RecalcWindowSafetyBonus; // move away if close, mild bonus if safe
    }
}
