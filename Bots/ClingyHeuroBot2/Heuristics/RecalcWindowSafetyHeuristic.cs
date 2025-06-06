#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class RecalcWindowSafetyHeuristic : IHeuristic
{
    public string Name => "RecalcWindowSafety";

    public decimal CalculateRawScore(GameState state, Animal me, BotAction move, ILogger? logger)
    {
        int ticksToRecalc = (20 - (state.Tick % 20)) % 20;
        if (ticksToRecalc > 3)
            return 0m; // evaluate only in last 3 ticks of window
        var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);
        int dist = state.Zookeepers.Any()
            ? state.Zookeepers.Min(z => Heuristics.ManhattanDistance(z.X, z.Y, nx, ny))
            : 999;
        return dist < 4 ? -1.5m / (dist + 1) : 0.4m; // move away if close, mild bonus if safe
    }
}
