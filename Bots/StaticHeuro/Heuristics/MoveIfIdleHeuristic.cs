using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace StaticHeuro.Heuristics;

public class MoveIfIdleHeuristic : IHeuristic
{
    public string Name => "MoveIfIdle";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var me = heuristicContext.CurrentAnimal;
        var (nx, ny) = heuristicContext.MyNewPosition;
        var weights = heuristicContext.Weights;

        // If the new position is the same as the current one, the bot is effectively idle.
        if (nx == me.X && ny == me.Y)
        {
            return -weights.MoveIfIdle; // Penalise idling
        }

        return 0m; // No score if we actually move somewhere
    }
}
