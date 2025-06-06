#pragma warning disable SKEXP0110
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics
{
    public class CenterDistanceBonusHeuristic : IHeuristic
    {
        public string Name => "CenterDistanceBonus";

        public decimal CalculateRawScore(
            GameState state,
            Animal me,
            BotAction move,
            ILogger? logger
        )
        {
            var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);
            var cx = state.Cells.Max(c => c.X) / 2;
            var cy = state.Cells.Max(c => c.Y) / 2;
            var dist = Heuristics.ManhattanDistance(nx, ny, cx, cy);
            return (1.0m / (dist + 1)) * 0.2m;
        }
    }
}
