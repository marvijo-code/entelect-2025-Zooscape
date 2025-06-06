using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class CenterControlHeuristic : IHeuristic
{
    public string Name => "CenterControl";

    public decimal CalculateRawScore(GameState state, Animal me, BotAction move, ILogger? logger)
    {
        int cx = (state.Cells.Min(c => c.X) + state.Cells.Max(c => c.X)) / 2;
        int cy = (state.Cells.Min(c => c.Y) + state.Cells.Max(c => c.Y)) / 2;
        var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);
        int distToCenter = Heuristics.ManhattanDistance(nx, ny, cx, cy);

        return distToCenter <= 2 ? 0.3m : 0m;
    }
}
