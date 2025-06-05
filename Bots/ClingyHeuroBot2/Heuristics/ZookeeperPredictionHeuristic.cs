#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using ClingyHeuroBot2; // For IHeuristic
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

// Assuming GetPotentialZookeeperNextPositions is in Heuristics.cs and accessible

namespace HeuroBot.Bots.ClingyHeuroBot2.Heuristics
{
    public class ZookeeperPredictionHeuristic : IHeuristic
    {
        public string Name => "ZookeeperPrediction";

        public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
        {
            var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);
            var zookeeperPositions = Heuristics.GetPotentialZookeeperNextPositions(state);

            foreach (var (zx, zy) in zookeeperPositions)
            {
                if (nx == zx && ny == zy)
                {
                    return -100m; // Strong penalty for moving into a zookeeper's predicted path
                }
            }
            return 0m;
        }
    }
}
