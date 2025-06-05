#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using HeuroBot.Services; // For Heuristics.HeuristicsImpl
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace ClingyHeuroBot2
{
    public class SpawnProximityHeuristic : IHeuristic
    {
        public string Name => "SpawnProximity";

        public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
        {
            var (nx, ny) = Heuristics.HeuristicsImpl.ApplyMove(me.X, me.Y, move);

            // Consider all unique animal spawn points
            var spawnPoints = state
                .Animals.Select(a => (X: a.SpawnX, Y: a.SpawnY))
                .Distinct()
                .ToList();

            if (!spawnPoints.Any())
                return 0m; // No spawn points to consider

            int minDistanceToSpawn = spawnPoints.Min(sp =>
                Heuristics.HeuristicsImpl.ManhattanDistance(sp.X, sp.Y, nx, ny)
            );

            if (me.CapturedCounter > 0 && minDistanceToSpawn <= 3)
            {
                // If recently captured, slightly reward moving near (but not necessarily onto) a spawn, perhaps for orientation or safety.
                return 0.5m;
            }

            if (minDistanceToSpawn <= 3)
            {
                // Generally, penalize being too close to spawn points if not just captured.
                return -1.0m;
            }

            return 0m;
        }
    }
}
