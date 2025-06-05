#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace ClingyHeuroBot2 // Updated namespace to root
{
    public class CenterControlHeuristic : IHeuristic
    {
        public string Name => "CenterControl";

        public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
        {
            int cx = (state.Cells.Min(c => c.X) + state.Cells.Max(c => c.X)) / 2;
            int cy = (state.Cells.Min(c => c.Y) + state.Cells.Max(c => c.Y)) / 2;
            var (nx, ny) = HeuroBot.Services.Heuristics.HeuristicsImpl.ApplyMove(me.X, me.Y, move);
            int distToCenter = HeuroBot.Services.Heuristics.HeuristicsImpl.ManhattanDistance(
                nx,
                ny,
                cx,
                cy
            );

            return distToCenter <= 2 ? 0.3m : 0m;
        }
    }
}
