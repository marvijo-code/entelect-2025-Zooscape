#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using HeuroBot.Services; // For Heuristics.HeuristicsImpl
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace ClingyHeuroBot2
{
    public class OpponentProximityHeuristic : IHeuristic
    {
        public string Name => "OpponentProximity";

        public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
        {
            var (nx, ny) = Heuristics.HeuristicsImpl.ApplyMove(me.X, me.Y, move);
            var dists = state.Zookeepers.Select(z =>
                Heuristics.HeuristicsImpl.ManhattanDistance(z.X, z.Y, nx, ny)
            );

            if (!dists.Any())
                return 0m; // No zookeepers, no proximity risk/score

            var minDist = dists.Min();

            // Score is higher if closer to a zookeeper (1 / (0+1) = 1, 1 / (1+1) = 0.5, etc.)
            // This raw score will likely be multiplied by a negative weight.
            if (minDist == 0) // Directly on a zookeeper's target square
            {
                return 10m; // High magnitude, sign determined by weight
            }
            return 1m / (decimal)minDist; // Use decimal for division
        }
    }
}
