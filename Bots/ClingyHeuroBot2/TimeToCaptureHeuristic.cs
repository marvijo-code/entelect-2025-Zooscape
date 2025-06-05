#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using HeuroBot.Services; // For Heuristics.HeuristicsImpl
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace ClingyHeuroBot2
{
    public class TimeToCaptureHeuristic : IHeuristic
    {
        public string Name => "TimeToCapture";

        public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
        {
            var (nx, ny) = Heuristics.HeuristicsImpl.ApplyMove(me.X, me.Y, move);

            // Find the zookeeper closest to the animal's current position
            var closestZookeeperToCurrentPos = state
                .Zookeepers.OrderBy(z =>
                    Heuristics.HeuristicsImpl.ManhattanDistance(z.X, z.Y, me.X, me.Y)
                )
                .FirstOrDefault();

            if (closestZookeeperToCurrentPos == null)
                return 0m; // No zookeepers, no time to capture

            // Estimate time to capture as the distance from this zookeeper to the animal's new position
            int timeEstimate = Heuristics.HeuristicsImpl.ManhattanDistance(
                closestZookeeperToCurrentPos.X,
                closestZookeeperToCurrentPos.Y,
                nx,
                ny
            );

            if (timeEstimate <= 2)
                return -2.0m; // Very high risk
            else if (timeEstimate <= 5)
                return -1.0m; // High risk
            else if (timeEstimate <= 10)
                return -0.5m; // Moderate risk

            // Low risk or moving away from the closest zookeeper relative to new position
            return 0.5m;
        }
    }
}
