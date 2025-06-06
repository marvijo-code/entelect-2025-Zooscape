#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System;
using System.Linq;
using ClingyHeuroBot2;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2
{
    public class PathSafetyHeuristic : IHeuristic
    {
        public string Name => "PathSafety";

        public decimal CalculateRawScore(
            GameState state,
            Animal me,
            BotAction move,
            ILogger? logger
        )
        {
            var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);

            decimal mobilityScore = Enum.GetValues<BotAction>()
                .Cast<BotAction>()
                .Count(nextAction =>
                {
                    var (x2, y2) = Heuristics.ApplyMove(nx, ny, nextAction);
                    return Heuristics.IsTraversable(state, x2, y2);
                });

            return mobilityScore <= 1 ? -1m : 0m;
        }
    }
}
