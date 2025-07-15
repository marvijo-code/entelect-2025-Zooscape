using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace StaticHeuro.Heuristics;

public class ReverseMovePenaltyHeuristic : IHeuristic
{
    public string Name => "ReverseMovePenalty";

    public decimal CalculateScore(IHeuristicContext context)
    {
        if (!context.PreviousAction.HasValue)
        {
            return 0;
        }

        if (IsOpposite(context.PreviousAction.Value, context.CurrentMove))
        {
            return context.Weights.ReverseMovePenalty;
        }

        return 0;
    }

    private bool IsOpposite(BotAction prev, BotAction current)
    {
        return (prev == BotAction.Up && current == BotAction.Down) ||
               (prev == BotAction.Down && current == BotAction.Up) ||
               (prev == BotAction.Left && current == BotAction.Right) ||
               (prev == BotAction.Right && current == BotAction.Left);
    }
}
