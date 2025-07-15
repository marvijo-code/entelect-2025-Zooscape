#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System;
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils;
using Serilog;

namespace StaticHeuro.Heuristics;

public class PathSafetyHeuristic : IHeuristic
{
    public string Name => "PathSafety";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var (nx, ny) = heuristicContext.MyNewPosition;

        decimal mobilityScore = Enum.GetValues<BotAction>()
            .Cast<BotAction>()
            .Count(nextAction =>
            {
                var (x2, y2) = BotUtils.ApplyMove(nx, ny, nextAction);
                return BotUtils.IsTraversable(heuristicContext.CurrentGameState, x2, y2);
            });

        return mobilityScore <= 1 ? -heuristicContext.Weights.PathSafetyPenalty : 0m;
    }
}
