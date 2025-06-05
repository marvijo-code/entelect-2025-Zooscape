#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using DeepMCTS.Enums;
using HeuroBot.Bots.ClingyHeuroBot2.Heuristics;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace ClingyHeuroBot2
{
    public class LineOfSightPelletsHeuristic : IHeuristic
    {
        public string Name => "LineOfSightPellets";

        public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
        {
            var (x, y) = HeuroBot.Services.Heuristics.HeuristicsImpl.ApplyMove(me.X, me.Y, move);
            int visPellets = 0;
            for (int i = 0; i < 6 && Heuristics.IsTraversable(state, x, y); i++)
            {
                if (state.Cells.Any(c => c.X == x && c.Y == y && c.Content == CellContent.Pellet))
                    visPellets++;
                (x, y) = Heuristics.ApplyMove(x, y, move);
            }
            return visPellets * 0.6m;
        }
    }
}
