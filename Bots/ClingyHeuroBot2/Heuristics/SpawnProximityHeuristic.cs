#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using ClingyHeuroBot2; // For IHeuristic
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace HeuroBot.Bots.ClingyHeuroBot2.Heuristics
{
    public class SpawnProximityHeuristic : IHeuristic
    {
        public string Name => "SpawnProximity";

        public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
        {
            var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);
            // Animal's own spawn point is me.SpawnX, me.SpawnY. No direct MySpawnPoint on GameState.

            int spawnDist = Heuristics.ManhattanDistance(me.SpawnX, me.SpawnY, nx, ny);

            if (spawnDist < 3 && state.Tick < 50)
            {
                return -1.0m * (3 - spawnDist);
            }

            return (decimal)spawnDist / 10.0m;
        }
    }
}
