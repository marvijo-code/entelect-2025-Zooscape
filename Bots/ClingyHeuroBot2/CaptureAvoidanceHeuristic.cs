#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using HeuroBot.Services; // For Heuristics.HeuristicsImpl
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace ClingyHeuroBot2
{
    public class CaptureAvoidanceHeuristic : IHeuristic
    {
        public string Name => "CaptureAvoidance";

        public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
        {
            var (nx, ny) = Heuristics.HeuristicsImpl.ApplyMove(me.X, me.Y, move);

            if (!state.Zookeepers.Any())
                return 0m; // No zookeepers, no capture to avoid

            bool amIThePrimaryTargetOverall = true;
            foreach (var zookeeper in state.Zookeepers)
            {
                int myDistanceToThisZookeeper = Heuristics.HeuristicsImpl.ManhattanDistance(
                    zookeeper.X,
                    zookeeper.Y,
                    me.X,
                    me.Y
                );
                bool anotherAnimalIsCloserToThisZookeeper = false;
                foreach (var otherAnimal in state.Animals.Where(a => a.IsViable && a.Id != me.Id))
                {
                    int theirDistanceToThisZookeeper = Heuristics.HeuristicsImpl.ManhattanDistance(
                        zookeeper.X,
                        zookeeper.Y,
                        otherAnimal.X,
                        otherAnimal.Y
                    );
                    if (theirDistanceToThisZookeeper < myDistanceToThisZookeeper)
                    {
                        anotherAnimalIsCloserToThisZookeeper = true;
                        break;
                    }
                }
                if (anotherAnimalIsCloserToThisZookeeper)
                {
                    amIThePrimaryTargetOverall = false; // If another animal is closer to *any* zookeeper, I'm not the sole primary target.
                    break;
                }
            }

            if (amIThePrimaryTargetOverall)
            {
                // If I am the primary target (or equally closest to all zookeepers compared to other animals),
                // base reaction on the first zookeeper.
                var firstZookeeper = state.Zookeepers.FirstOrDefault();
                // No null check needed for firstZookeeper due to initial !state.Zookeepers.Any() check

                int currentDistToFirstZk = Heuristics.HeuristicsImpl.ManhattanDistance(
                    firstZookeeper.X,
                    firstZookeeper.Y,
                    me.X,
                    me.Y
                );
                int newDistToFirstZk = Heuristics.HeuristicsImpl.ManhattanDistance(
                    firstZookeeper.X,
                    firstZookeeper.Y,
                    nx,
                    ny
                );

                if (newDistToFirstZk > currentDistToFirstZk)
                    return 2.0m; // Rewarded for moving away
                else if (newDistToFirstZk < currentDistToFirstZk)
                    return -2.0m; // Penalized for moving closer
            }

            return 0m;
        }
    }
}
