#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System;
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class MobilityHeuristic : IHeuristic
{
    public string Name => "Mobility";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var (nx, ny) = heuristicContext.MyNewPosition;

        var mobilityCount = Enum.GetValues<BotAction>()
            .Cast<BotAction>()
            .Count(nextAction =>
            {
                var (x2, y2) = BotUtils.ApplyMove(nx, ny, nextAction);
                return BotUtils.IsTraversable(heuristicContext.CurrentGameState, x2, y2);
            });

        return mobilityCount * heuristicContext.Weights.Mobility;
    }
}
