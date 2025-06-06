#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using HeuroBot.Services; // For Heuristics.HeuristicsImpl
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace ClingyHeuroBot2
{
    public class ZookeeperPredictionHeuristic : IHeuristic
    {
        public string Name => "ZookeeperPrediction";

        public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
        {
            var (nx, ny) = Heuristics.HeuristicsImpl.ApplyMove(me.X, me.Y, move);
            var zookeeperNextPositions =
                Heuristics.HeuristicsImpl.GetPotentialZookeeperNextPositions(state);

            if (!zookeeperNextPositions.Any()) // No zookeepers or no predictable positions
                return 0m;

            if (zookeeperNextPositions.Any(pos => pos.x == nx && pos.y == ny))
                return -3.0m; // Strong penalty for moving into a predicted zookeeper square

            // Calculate minimum distance to any predicted zookeeper position
            // Ensure zookeeperNextPositions is not empty before calling Min, though already checked by !Any()
            int minDist = zookeeperNextPositions.Min(pos =>
                Heuristics.HeuristicsImpl.ManhattanDistance(pos.x, pos.y, nx, ny)
            );

            if (minDist <= 2) // If close to a predicted zookeeper square
                return -1.5m / ((decimal)minDist + 0.5m); // Penalty inversely proportional to distance

            return 0m; // No significant threat from predicted zookeeper positions
        }
    }
}
