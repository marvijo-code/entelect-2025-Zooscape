#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System;
using System.Linq;
using HeuroBot.Services; // For Heuristics.HeuristicsImpl
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace ClingyHeuroBot2
{
    public class PathSafetyHeuristic : IHeuristic
    {
        public string Name => "PathSafety";

        public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
        {
            // Replicate the logic of HeuristicsImpl.Mobility directly
            var (nx, ny) = Heuristics.HeuristicsImpl.ApplyMove(me.X, me.Y, move);

            int mobilityScore = Enum.GetValues<BotAction>()
                .Cast<BotAction>()
                .Count(nextAction =>
                {
                    var (x2, y2) = Heuristics.HeuristicsImpl.ApplyMove(nx, ny, nextAction);
                    return Heuristics.HeuristicsImpl.IsTraversable(state, x2, y2);
                });

            // Original PathSafety logic: Mobility(state, me, m) <= 1 ? -1m : 0m;
            return mobilityScore <= 1 ? -1m : 0m;
        }
    }
}
