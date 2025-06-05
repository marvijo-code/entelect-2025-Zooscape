#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace ClingyHeuroBot2 // Updated namespace to root
{
    public class WallCollisionRiskHeuristic : IHeuristic
    {
        public string Name => "WallCollisionRisk";

        public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
        {
            var (x, y) = HeuroBot.Services.Heuristics.HeuristicsImpl.ApplyMove(me.X, me.Y, move);
            int steps = 0;
            while (
                steps < 3 && HeuroBot.Services.Heuristics.HeuristicsImpl.IsTraversable(state, x, y)
            )
            {
                (x, y) = HeuroBot.Services.Heuristics.HeuristicsImpl.ApplyMove(x, y, move);
                steps++;
            }
            if (!HeuroBot.Services.Heuristics.HeuristicsImpl.IsTraversable(state, x, y))
                steps--;

            return steps switch
            {
                0 => -2.0m, // would hit wall next tick
                1 => -0.8m,
                2 => -0.3m,
                _ => 0m,
            };
        }
    }
}
