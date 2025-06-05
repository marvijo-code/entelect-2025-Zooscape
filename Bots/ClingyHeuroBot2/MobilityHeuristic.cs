#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System;
using System.Linq;
using HeuroBot.Services; // For Heuristics.HeuristicsImpl
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace ClingyHeuroBot2
{
    public class MobilityHeuristic : IHeuristic
    {
        public string Name => "Mobility";

        public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
        {
            var (nx, ny) = Heuristics.HeuristicsImpl.ApplyMove(me.X, me.Y, move);

            // Count the number of valid moves from the new position (nx, ny)
            return Enum.GetValues<BotAction>()
                .Cast<BotAction>()
                .Count(nextAction =>
                {
                    var (x2, y2) = Heuristics.HeuristicsImpl.ApplyMove(nx, ny, nextAction);
                    return Heuristics.HeuristicsImpl.IsTraversable(state, x2, y2);
                });
        }
    }
}
