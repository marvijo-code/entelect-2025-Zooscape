#pragma warning disable SKEXP0110
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils; // Added
using Serilog;

namespace ClingyHeuroBot2.Heuristics
{
    public class AreaControlHeuristic : IHeuristic
    {
        public string Name => "AreaControl";

        public decimal CalculateScore(IHeuristicContext heuristicContext)
        {
            var (nx, ny) = heuristicContext.MyNewPosition; // Updated
            decimal value = 0;

            if (heuristicContext.CurrentGameState.Cells == null)
                return 0m;

            foreach (
                var cell in heuristicContext.CurrentGameState.Cells.Where(c =>
                    c.Content == CellContent.Pellet
                )
            )
            {
                int dist = BotUtils.ManhattanDistance(cell.X, cell.Y, nx, ny); // Updated
                if (dist <= 5) // Increased radius to 5
                    value += 1.0m / (dist + 1); // Weight by inverse distance
            }
            return value;
        }
    }
}
