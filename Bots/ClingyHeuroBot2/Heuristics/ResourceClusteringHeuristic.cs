#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using ClingyHeuroBot2;
using DeepMCTS.Enums; // For CellContent
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace HeuroBot.Bots.ClingyHeuroBot2.Heuristics
{
    public class ResourceClusteringHeuristic : IHeuristic
    {
        public string Name => "ResourceClustering";

        public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
        {
            var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);

            if (state.Cells == null)
                return 0m;

            // Count pellets in a larger radius (3 instead of 2) to be more aware of clusters
            int pelletCount = state.Cells.Count(c =>
                c.Content == CellContent.Pellet
                && Heuristics.ManhattanDistance(c.X, c.Y, nx, ny) <= 3
            );

            // Give immediate cells higher weight
            int immediatePellets = state.Cells.Count(c =>
                c.Content == CellContent.Pellet
                && Heuristics.ManhattanDistance(c.X, c.Y, nx, ny) <= 1
            );

            // Prioritize moves that put us directly on a pellet or next to one
            return pelletCount + (immediatePellets * 2.0m);
        }
    }
}
