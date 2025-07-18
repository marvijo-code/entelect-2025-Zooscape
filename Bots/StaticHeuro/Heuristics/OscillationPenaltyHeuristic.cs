using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace StaticHeuro.Heuristics;

/// <summary>
/// Penalises 2–step oscillations such as Left↔Right or Up↔Down loops by looking at the
/// last two committed directions for this animal.  If the current candidate move would
/// create the same position as two ticks ago, or if the move is the direct opposite of
/// the previous committed move, we apply a configurable penalty.
/// </summary>
public class OscillationPenaltyHeuristic : IHeuristic
{
    public string Name => "OscillationPenalty";

    public decimal CalculateScore(IHeuristicContext ctx)
    {
        var recentPositions = ctx.AnimalRecentPositions;
        if (recentPositions == null || recentPositions.Count < 3)
            return 0m;

        // recentPositions holds FIFO queue; convert to array for index.
        var posArr = recentPositions.ToArray();
        var twoTicksAgo = posArr[^3]; // third from end (count-3)

        var (nx, ny) = ctx.MyNewPosition;
        if (nx == twoTicksAgo.Item1 && ny == twoTicksAgo.Item2)
        {
            // Candidate would return to the cell visited two ticks earlier => oscillation detected.
            return -ctx.Weights.ReverseMovePenalty * 2; // reuse existing weight, doubled for stronger effect
        }

        // Also penalise direct reversal of last committed direction
        if (ctx.AnimalLastDirection.HasValue && IsOpposite(ctx.AnimalLastDirection.Value, ctx.CurrentMove))
        {
            return -ctx.Weights.ReverseMovePenalty; // single weight
        }

        return 0m;
    }

    private static bool IsOpposite(BotAction a, BotAction b) =>
        (a == BotAction.Left && b == BotAction.Right) ||
        (a == BotAction.Right && b == BotAction.Left) ||
        (a == BotAction.Up && b == BotAction.Down) ||
        (a == BotAction.Down && b == BotAction.Up);
}
