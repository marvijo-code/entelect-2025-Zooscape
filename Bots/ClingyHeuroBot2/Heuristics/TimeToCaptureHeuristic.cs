#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using ClingyHeuroBot2; // For IHeuristic
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace HeuroBot.Bots.ClingyHeuroBot2.Heuristics
{
    public class TimeToCaptureHeuristic : IHeuristic
    {
        public string Name => "TimeToCapture";

        public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
        {
            var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);

            if (!state.Zookeepers.Any())
            {
                return 0m;
            }

            var zookeeper = state
                .Zookeepers.OrderBy(z => Heuristics.ManhattanDistance(z.X, z.Y, nx, ny))
                .First();
            int distToZookeeper = Heuristics.ManhattanDistance(zookeeper.X, zookeeper.Y, nx, ny);

            if (distToZookeeper <= 2)
            {
                return -5.0m * (3 - distToZookeeper); // Strong penalty for being very close
            }

            return (decimal)distToZookeeper * 0.5m; // General score based on distance
        }
    }
}
