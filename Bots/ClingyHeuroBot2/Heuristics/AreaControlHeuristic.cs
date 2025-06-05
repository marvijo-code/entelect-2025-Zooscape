#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using ClingyHeuroBot2;
using DeepMCTS.Enums; // For CellContent
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace HeuroBot.Bots.ClingyHeuroBot2.Heuristics
{
    public class AreaControlHeuristic : IHeuristic
    {
        public string Name => "AreaControl";

        public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
        {
            var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);
            decimal value = 0;

            if (state.Cells == null)
                return 0m;

            foreach (var cell in state.Cells.Where(c => c.Content == CellContent.Pellet))
            {
                int dist = Heuristics.ManhattanDistance(cell.X, cell.Y, nx, ny);
                if (dist <= 5) // Increased radius to 5
                    value += 1.0m / (dist + 1); // Weight by inverse distance
            }
            return value;
        }
    }
}
