#pragma warning disable SKEXP0110
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics
{
    public class CaptureAvoidanceHeuristic : IHeuristic
    {
        public string Name => "CaptureAvoidance";

        public decimal CalculateRawScore(
            GameState state,
            Animal me,
            BotAction move,
            ILogger? logger
        )
        {
            var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);
            bool amITarget = false;
            Zookeeper? targetingZookeeper = null;

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
                    return 2.0m;
                else if (newDist < currentDist)
                    return -2.0m;
            }
            return 0m;
        }
    }
}
