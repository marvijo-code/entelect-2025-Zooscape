// #pragma warning disable SKEXP0110 // Disable SK Experimental warning
using System.Linq;
using HeuroBot.Services; // For Heuristics.HeuristicsImpl static methods
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace HeuroBot.Bots.ClingyHeuroBot2.Heuristics;

public class TargetEvaluationHeuristic : IHeuristic
{
    public string Name => "TargetEvaluation";

    public decimal CalculateRawScore(GameState state, Animal me, BotAction move, ILogger? logger)
    {
        if (!state.Zookeepers.Any())
        {
            return 0m;
        }

        var (nx, ny) = Heuristics.HeuristicsImpl.ApplyMove(me.X, me.Y, move);

        var zookeeper = state.Zookeepers.First();
        var viableAnimals = state.Animals.Where(a => a.IsViable).ToList();

        if (!viableAnimals.Any())
        {
            return 0m;
        }

        int myDistance = Heuristics.HeuristicsImpl.ManhattanDistance(
            zookeeper.X,
            zookeeper.Y,
            me.X,
            me.Y
        );
        bool amITarget = true;

        foreach (var animal in viableAnimals.Where(a => a.Id != me.Id))
        {
            int theirDistance = Heuristics.HeuristicsImpl.ManhattanDistance(
                zookeeper.X,
                zookeeper.Y,
                animal.X,
                animal.Y
            );
            if (theirDistance < myDistance)
            {
                amITarget = false;
                break;
            }
        }

        if (amITarget)
        {
            int newDist = Heuristics.HeuristicsImpl.ManhattanDistance(
                zookeeper.X,
                zookeeper.Y,
                nx,
                ny
            );
            // If I am the target, moving further from the zookeeper is good, moving closer is bad.
            return newDist > myDistance ? 1.5m : -1.5m;
        }
        else
        {
            // If I am not the target, return a small positive score.
            return 0.5m;
        }
    }
}
