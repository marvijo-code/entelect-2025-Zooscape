#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using ClingyHeuroBot2;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace HeuroBot.Bots.ClingyHeuroBot2.Heuristics
{
    public class OpponentProximityHeuristic : IHeuristic
    {
        public string Name => "OpponentProximity";

        public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
        {
            // Calls to ApplyMove and ManhattanDistance are to static methods in the main Heuristics class
            var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);

            if (state.Zookeepers == null || !state.Zookeepers.Any())
                return 0m; // No zookeepers, no proximity risk/reward

            var dists = state.Zookeepers.Select(z =>
                Heuristics.ManhattanDistance(z.X, z.Y, nx, ny)
            );

            if (!dists.Any())
                return 0m;

            var minDist = dists.Min();

            if (minDist < 0)
                return 0m; // Should not happen with Manhattan distance

            return 1m / (minDist + 1.0m); // Ensure decimal division
        }
    }
}
