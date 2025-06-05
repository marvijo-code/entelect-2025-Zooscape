#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
using System.Linq;
using DeepMCTS.Enums; // For CellContent
using HeuroBot.Services; // For Heuristics.HeuristicsImpl
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace ClingyHeuroBot2
{
    public class QuadrantAwarenessHeuristic : IHeuristic
    {
        public string Name => "QuadrantAwareness";

        public decimal CalculateRawScore(GameState state, Animal me, BotAction move)
        {
            var (nx, ny) = Heuristics.HeuristicsImpl.ApplyMove(me.X, me.Y, move);

            if (!state.Cells.Any())
                return 0m; // Should not happen

            int minX = state.Cells.Min(c => c.X);
            int maxX = state.Cells.Max(c => c.X);
            int minY = state.Cells.Min(c => c.Y);
            int maxY = state.Cells.Max(c => c.Y);

            // Integer division is fine for determining center for quadrant logic
            int centerX = (minX + maxX) / 2;
            int centerY = (minY + maxY) / 2;

            // Determine the quadrant of the new position (nx, ny)
            // Quadrant mapping:
            // 0: bottom-left (x < centerX, y < centerY)
            // 1: bottom-right (x >= centerX, y < centerY)
            // 2: top-left (x < centerX, y >= centerY)
            // 3: top-right (x >= centerX, y >= centerY)
            int targetQuadrant = (nx >= centerX ? 1 : 0) + (ny >= centerY ? 2 : 0);

            int[] pelletsByQuadrant = new int[4];
            foreach (var cell in state.Cells.Where(c => c.Content == CellContent.Pellet))
            {
                int q = (cell.X >= centerX ? 1 : 0) + (cell.Y >= centerY ? 2 : 0);
                pelletsByQuadrant[q]++;
            }

            int[] animalsByQuadrant = new int[4];
            foreach (var animal in state.Animals.Where(a => a.Id != me.Id && a.IsViable))
            {
                int q = (animal.X >= centerX ? 1 : 0) + (animal.Y >= centerY ? 2 : 0);
                animalsByQuadrant[q]++;
            }

            decimal quadrantValue = pelletsByQuadrant[targetQuadrant] * 0.2m;
            if (animalsByQuadrant[targetQuadrant] > 0)
            {
                quadrantValue -= animalsByQuadrant[targetQuadrant] * 0.1m;
            }

            return quadrantValue;
        }
    }
}
