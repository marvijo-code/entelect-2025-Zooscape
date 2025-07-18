// #pragma warning disable SKEXP0110 // Disable SK Experimental warning
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils;
using Serilog;

namespace StaticHeuro.Heuristics;

public class ZookeeperCooldownHeuristic : IHeuristic
{
    public string Name => "ZookeeperCooldown";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var weights = heuristicContext.Weights;
        var state = heuristicContext.CurrentGameState;
        var me = heuristicContext.CurrentAnimal;

        if (state == null || me == null || !state.Zookeepers.Any() || !state.Animals.Any())
        {
            return 0m;
        }

        decimal score = 0m;
        var recalculateInterval = weights.ZookeeperCooldownRecalcInterval;

        // Guard against misconfigured weight that could cause divide-by-zero
        if (recalculateInterval == 0)
        {
            // heuristicContext.Logger?.Warning("{HeuristicName}: ZookeeperCooldownRecalcInterval is zero. Skipping cooldown bonus calculation to avoid division by zero.", Name);
            return 0m;
        }

        int ticksUntilRecalculate = recalculateInterval - (state.Tick % recalculateInterval);

        foreach (var zookeeper in state.Zookeepers)
        {
            // Find the zookeeper's current target by finding the nearest animal that is not in its cage.
            Animal? currentTarget = state.Animals
                .Where(a => !(a.X == a.SpawnX && a.Y == a.SpawnY)) // Exclude animals in cages
                .OrderBy(a => BotUtils.ManhattanDistance(zookeeper.X, zookeeper.Y, a.X, a.Y))
                .FirstOrDefault();

            // If the zookeeper is targeting another animal, it's safer for us.
            if (currentTarget != null && currentTarget.Id != me.Id)
            {
                // The bonus is higher if the recalculation is further away.
                // This encourages taking advantage of the "cooldown" period.
                score += weights.ZookeeperCooldownBonus * ((decimal)ticksUntilRecalculate / recalculateInterval); // recalculateInterval guaranteed non-zero
            }
        }

        // Normalize the score by the number of zookeepers to keep it within a reasonable range.
        return state.Zookeepers.Count > 0 ? score / state.Zookeepers.Count : 0m;
    }
}
