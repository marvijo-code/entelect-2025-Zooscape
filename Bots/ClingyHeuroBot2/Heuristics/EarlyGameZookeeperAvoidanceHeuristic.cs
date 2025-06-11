#pragma warning disable SKEXP0110
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class EarlyGameZookeeperAvoidanceHeuristic : IHeuristic
{
    public string Name => "EarlyGameZookeeperAvoidance";

    public decimal CalculateRawScore(IHeuristicContext heuristicContext)
    {
        const int earlyGameTicks = 300; // 15% of 2000 ticks

        if (heuristicContext.CurrentGameState.Tick > earlyGameTicks || !heuristicContext.CurrentGameState.Zookeepers.Any())
        {
            return 0m;
        }

        var (nx, ny) = heuristicContext.MyNewPosition;
        int minDistance = heuristicContext.CurrentGameState.Zookeepers
            .Min(z => Heuristics.ManhattanDistance(z.X, z.Y, nx, ny));

        if (minDistance < 10)
        {
            // Strong penalty for being close to a zookeeper in the early game.
            // The penalty is higher the closer the zookeeper is.
            return -1.0m * (10 - minDistance);
        }

        return 0m;
    }
}
