#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using ClingyHeuroBot2;
using DeepMCTS.Enums; // For CellContent
using HeuroBot.Services; // For Heuristics.HeuristicsImpl
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2
{
    public class ResourceClusteringHeuristic : IHeuristic
    {
        public string Name => "ResourceClustering";

        public decimal CalculateRawScore(
            GameState state,
            Animal me,
            BotAction move,
            ILogger? logger
        )
        {
            var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);

            if (state.Cells == null)
                return 0m;

            int pelletCount = state.Cells.Count(c =>
                c.Content == CellContent.Pellet
                && Heuristics.ManhattanDistance(c.X, c.Y, nx, ny) <= 3
            );

            int immediatePellets = state.Cells.Count(c =>
                c.Content == CellContent.Pellet
                && Heuristics.ManhattanDistance(c.X, c.Y, nx, ny) <= 1
            );

            return pelletCount + (immediatePellets * 2.0m);
        }
    }
}
