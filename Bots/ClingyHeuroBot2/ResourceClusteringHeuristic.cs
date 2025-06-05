#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using DeepMCTS.Enums; // For CellContent
using HeuroBot.Services; // For Heuristics.HeuristicsImpl
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace ClingyHeuroBot2
{
    public class ResourceClusteringHeuristic : IHeuristic
    {
        public string Name => "ResourceClustering";

        public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
        {
            var (nx, ny) = Heuristics.HeuristicsImpl.ApplyMove(me.X, me.Y, move);

            // Count pellets in a larger radius (3) to be more aware of clusters
            int pelletCountRadius3 = state.Cells.Count(c =>
                c.Content == CellContent.Pellet
                && Heuristics.HeuristicsImpl.ManhattanDistance(c.X, c.Y, nx, ny) <= 3
            );

            // Give immediate cells (radius 1) higher weight
            int immediatePelletsRadius1 = state.Cells.Count(c =>
                c.Content == CellContent.Pellet
                && Heuristics.HeuristicsImpl.ManhattanDistance(c.X, c.Y, nx, ny) <= 1
            );

            // Prioritize moves that put us directly on a pellet or next to one
            return (decimal)pelletCountRadius3 + ((decimal)immediatePelletsRadius1 * 2.0m);
        }
    }
}
