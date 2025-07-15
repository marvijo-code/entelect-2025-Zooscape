#pragma warning disable SKEXP0110
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Marvijo.Zooscape.Bots.Common.Utils; // Added
using Serilog;

namespace ClingyHeuroBot2.Heuristics
{
    public class CornerControlHeuristic : IHeuristic
    {
        public string Name => "CornerControl";

        public decimal CalculateScore(IHeuristicContext heuristicContext)
        {
            var (nx, ny) = heuristicContext.MyNewPosition; // Updated

            int maxX = heuristicContext.CurrentGameState.Cells.Max(c => c.X);
            int maxY = heuristicContext.CurrentGameState.Cells.Max(c => c.Y);

            var corners = new[] { (0, 0), (0, maxY), (maxX, 0), (maxX, maxY) };

            var minDistance = corners.Min(c =>
                BotUtils.ManhattanDistance(nx, ny, c.Item1, c.Item2) // Updated
            );
            return (1.0m / (minDistance + 1)) * 0.1m;
        }
    }
}
