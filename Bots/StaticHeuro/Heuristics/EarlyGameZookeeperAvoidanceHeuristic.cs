#pragma warning disable SKEXP0110
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Utils;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace StaticHeuro.Heuristics;

public class EarlyGameZookeeperAvoidanceHeuristic : IHeuristic
{
    public string Name => "EarlyGameZookeeperAvoidance";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        const int earlyGameTicks = 300; // 15% of 2000 ticks

        if (heuristicContext.CurrentGameState.Tick > earlyGameTicks || !heuristicContext.CurrentGameState.Zookeepers.Any())
        {
            return 0m;
        }

        var (nx, ny) = heuristicContext.MyNewPosition;
        int minDistance = heuristicContext.CurrentGameState.Zookeepers
            .Min(z => BotUtils.ManhattanDistance(z.X, z.Y, nx, ny));

        if (minDistance < 10)
        {
            // Apply graduated penalties based on distance
            if (minDistance < 5)
            {
                // Very strong penalty for being very close to a zookeeper (< 5 tiles)
                return -heuristicContext.Weights.EarlyGameZookeeperAvoidancePenalty * (10 - minDistance) * 1.5m;
            }
            else
            {
                // Reduced penalty for more distant zookeepers (5-9 tiles)
                return -heuristicContext.Weights.EarlyGameZookeeperAvoidancePenalty * (10 - minDistance) * 0.7m;
            }
        }

        return 0m;
    }
}
