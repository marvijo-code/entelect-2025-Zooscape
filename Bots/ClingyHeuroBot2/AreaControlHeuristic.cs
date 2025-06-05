#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using DeepMCTS.Enums; // For CellContent
using HeuroBot.Services; // For Heuristics.HeuristicsImpl
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace ClingyHeuroBot2
{
    public class AreaControlHeuristic : IHeuristic
    {
        public string Name => "AreaControl";

        public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
        {
            var (nx, ny) = Heuristics.HeuristicsImpl.ApplyMove(me.X, me.Y, move);
            decimal value = 0m;

            foreach (var cell in state.Cells.Where(c => c.Content == CellContent.Pellet))
            {
                int dist = Heuristics.HeuristicsImpl.ManhattanDistance(cell.X, cell.Y, nx, ny);
                if (dist <= 5) // Considers pellets within a radius of 5
                {
                    value += 1.0m / (dist + 1); // Weight by inverse distance (closer pellets contribute more)
                }
            }
            return value;
        }
    }
}
