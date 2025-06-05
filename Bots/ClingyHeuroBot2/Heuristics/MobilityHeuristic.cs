#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System;
using System.Linq;
using ClingyHeuroBot2;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace HeuroBot.Bots.ClingyHeuroBot2.Heuristics
{
    public class MobilityHeuristic : IHeuristic
    {
        public string Name => "Mobility";

        public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
        {
            var (nx, ny) = Heuristics.ApplyMove(me.X, me.Y, move);

            // After the initial move, count how many subsequent moves are possible
            return Enum.GetValues<BotAction>()
                .Cast<BotAction>()
                .Count(nextAction =>
                {
                    var (x2, y2) = Heuristics.ApplyMove(nx, ny, nextAction);
                    return Heuristics.IsTraversable(state, x2, y2);
                });
        }
    }
}
