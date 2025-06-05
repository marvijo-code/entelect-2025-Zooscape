// #pragma warning disable SKEXP0110 // Disable SK Experimental warning
using System.Linq;
using HeuroBot.Services; // For Heuristics.HeuristicsImpl static methods
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace HeuroBot.Bots.ClingyHeuroBot2.Heuristics;

public class ZookeeperCooldownHeuristic : IHeuristic
{
    public string Name => "ZookeeperCooldown";

    public decimal CalculateRawScore(GameState state, Animal me, BotAction move, ILogger? logger)
    {
        // This heuristic encourages moving away from the zookeeper if it's near its action/movement window.
        // The original logic used `state.Tick % 20`. If recalcTick is 18, 19 (>=18) or 0, it activates.
        int recalcTick = state.Tick % 20;

        if (recalcTick >= 18 || recalcTick == 0) // Activates on ticks 18, 19, 0, 1, ... (0 is start of new 20-tick cycle)
        {
            var zookeeper = state.Zookeepers.FirstOrDefault();
            if (zookeeper != null)
            {
                var (nx, ny) = Heuristics.HeuristicsImpl.ApplyMove(me.X, me.Y, move);
                int currentDist = Heuristics.HeuristicsImpl.ManhattanDistance(
                    zookeeper.X,
                    zookeeper.Y,
                    me.X,
                    me.Y
                );
                int newDist = Heuristics.HeuristicsImpl.ManhattanDistance(
                    zookeeper.X,
                    zookeeper.Y,
                    nx,
                    ny
                );

                if (newDist > currentDist)
                {
                    return 1.5m; // Good to move away when zookeeper might act
                }
            }
        }

        return 0m; // No bonus if not in the zookeeper's action window or if move doesn't increase distance
    }
}
