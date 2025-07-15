#pragma warning disable SKEXP0110
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils;
using Serilog;

namespace StaticHeuro.Heuristics;

public class CaptureAvoidanceHeuristic : IHeuristic
{
    public string Name => "CaptureAvoidance";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var (nx, ny) = heuristicContext.MyNewPosition;
        bool amITarget = false;
        Zookeeper? targetingZookeeper = null;

        foreach (var zookeeper in heuristicContext.CurrentGameState.Zookeepers)
        {
            var animalsByDistance = heuristicContext
                .CurrentGameState.Animals.Where(a => a.IsViable)
                .OrderBy(a => BotUtils.ManhattanDistance(zookeeper.X, zookeeper.Y, a.X, a.Y));

            if (animalsByDistance.FirstOrDefault()?.Id == heuristicContext.CurrentAnimal.Id)
            {
                amITarget = true;
                targetingZookeeper = zookeeper;
                break;
            }
        }

        if (amITarget && targetingZookeeper != null)
        {
            int currentDist = BotUtils.ManhattanDistance(
                targetingZookeeper.X,
                targetingZookeeper.Y,
                heuristicContext.CurrentAnimal.X,
                heuristicContext.CurrentAnimal.Y
            );
            int newDist = BotUtils.ManhattanDistance(
                targetingZookeeper.X,
                targetingZookeeper.Y,
                nx,
                ny
            );

            // If the new distance is less than the current one, we are moving towards danger.
            if (newDist < currentDist)
            {
                // The penalty should be inversely proportional to the distance. Closer zookeepers are a bigger threat.
                // Avoid division by zero.
                if (newDist == 0) return -10000m; 
                
                // As newDist approaches 1, the penalty approaches its maximum.
                var dangerProximityFactor = 1.0m / newDist;
                return -heuristicContext.Weights.CaptureAvoidancePenaltyFactor * dangerProximityFactor;
            }
            // If the new distance is greater, we are moving away from danger.
            else if (newDist > currentDist)
            {
                // Reward moving away, but scale the reward by the distance.
                // The further away the zookeeper, the less important it is to move away.
                if (currentDist == 0) return heuristicContext.Weights.CaptureAvoidanceRewardFactor; // Should not happen if newDist > currentDist
                return heuristicContext.Weights.CaptureAvoidanceRewardFactor / currentDist;
            }
        }
        return 0m;
    }
}
