#pragma warning disable SKEXP0110
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class CaptureAvoidanceHeuristic : IHeuristic
{
    public string Name => "CaptureAvoidance";

    public decimal CalculateRawScore(IHeuristicContext heuristicContext)
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

            if (newDist > currentDist)
                return 2.0m;
            else if (newDist < currentDist)
                return -2.0m;
        }
        return 0m;
    }
}
