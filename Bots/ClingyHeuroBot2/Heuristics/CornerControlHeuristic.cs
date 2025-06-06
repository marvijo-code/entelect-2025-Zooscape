#pragma warning disable SKEXP0110
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics
{
    public class CornerControlHeuristic : IHeuristic
    {
        public string Name => "CornerControl";

        public decimal CalculateRawScore(
            GameState state,
            Animal me,
            BotAction move,
            ILogger? logger
        )
        {
            var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);
            var corners = new[]
            {
                (0, 0),
                (0, state.Cells.Max(c => c.Y)),
                (state.Cells.Max(c => c.X), 0),
                (state.Cells.Max(c => c.X), state.Cells.Max(c => c.Y)),
            };

            var minDistance = corners.Min(c =>
                Heuristics.ManhattanDistance(nx, ny, c.Item1, c.Item2)
            );
            return (1.0m / (minDistance + 1)) * 0.1m;
        }
    }
}
