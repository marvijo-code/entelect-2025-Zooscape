#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using ClingyHeuroBot2; // For IHeuristic
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace HeuroBot.Bots.ClingyHeuroBot2.Heuristics
{
    public class CaptureAvoidanceHeuristic : IHeuristic
    {
        public string Name => "CaptureAvoidance";

        public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
        {
            var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);
            bool amITarget = false;
            Zookeeper? targetingZookeeper = null;

            // Determine if 'me' is the current target for any zookeeper
            foreach (var zookeeper in state.Zookeepers)
            {
                var animalsByDistance = state
                    .Animals.Where(a => a.IsViable)
                    .OrderBy(a => Heuristics.ManhattanDistance(zookeeper.X, zookeeper.Y, a.X, a.Y));

                if (animalsByDistance.FirstOrDefault()?.Id == me.Id)
                {
                    amITarget = true;
                    targetingZookeeper = zookeeper;
                    break;
                }
            }

            if (amITarget && targetingZookeeper != null)
            {
                int currentDist = Heuristics.ManhattanDistance(
                    targetingZookeeper.X,
                    targetingZookeeper.Y,
                    me.X,
                    me.Y
                );
                int newDist = Heuristics.ManhattanDistance(
                    targetingZookeeper.X,
                    targetingZookeeper.Y,
                    nx,
                    ny
                );

                if (newDist > currentDist)
                    return 2.0m; // Moving away from targetting zookeeper
                else if (newDist < currentDist)
                    return -2.0m; // Moving towards targetting zookeeper
            }
            return 0m;
        }
    }
}
