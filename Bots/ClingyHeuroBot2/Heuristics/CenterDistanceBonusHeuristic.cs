#pragma warning disable SKEXP0110
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils; // Added
using Serilog;

namespace ClingyHeuroBot2.Heuristics
{
    public class CenterDistanceBonusHeuristic : IHeuristic
    {
        public string Name => "CenterDistanceBonus";

        public decimal CalculateRawScore(IHeuristicContext heuristicContext)
        {
            var (nx, ny) = heuristicContext.MyNewPosition; // Updated
            var cx = heuristicContext.CurrentGameState.Cells.Max(c => c.X) / 2;
            var cy = heuristicContext.CurrentGameState.Cells.Max(c => c.Y) / 2;
            var dist = BotUtils.ManhattanDistance(nx, ny, cx, cy); // Updated
            return (1.0m / (dist + 1)) * 0.2m;
        }
    }
}
