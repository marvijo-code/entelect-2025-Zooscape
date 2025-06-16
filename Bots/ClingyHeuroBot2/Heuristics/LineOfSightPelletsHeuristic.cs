#pragma warning disable SKEXP0110
using System.Linq;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace ClingyHeuroBot2.Heuristics;

public class LineOfSightPelletsHeuristic : IHeuristic
{
    public string Name => "LineOfSightPellets";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var (nx, ny) = heuristicContext.MyNewPosition;
        int pelletsInSight = 0;

        // Check horizontal line of sight
        for (int x = nx + 1; x < heuristicContext.CurrentGameState.Cells.Max(c => c.X); x++)
        {
            var cell = heuristicContext.CurrentGameState.Cells.FirstOrDefault(c =>
                c.X == x && c.Y == ny
            );
            if (cell == null || cell.Content == CellContent.Wall)
                break;
            if (cell.Content == CellContent.Pellet)
                pelletsInSight++;
        }
        for (int x = nx - 1; x >= 0; x--)
        {
            var cell = heuristicContext.CurrentGameState.Cells.FirstOrDefault(c =>
                c.X == x && c.Y == ny
            );
            if (cell == null || cell.Content == CellContent.Wall)
                break;
            if (cell.Content == CellContent.Pellet)
                pelletsInSight++;
        }

        // Check vertical line of sight
        for (int y = ny + 1; y < heuristicContext.CurrentGameState.Cells.Max(c => c.Y); y++)
        {
            var cell = heuristicContext.CurrentGameState.Cells.FirstOrDefault(c =>
                c.X == nx && c.Y == y
            );
            if (cell == null || cell.Content == CellContent.Wall)
                break;
            if (cell.Content == CellContent.Pellet)
                pelletsInSight++;
        }
        for (int y = ny - 1; y >= 0; y--)
        {
            var cell = heuristicContext.CurrentGameState.Cells.FirstOrDefault(c =>
                c.X == nx && c.Y == y
            );
            if (cell == null || cell.Content == CellContent.Wall)
                break;
            if (cell.Content == CellContent.Pellet)
                pelletsInSight++;
        }

        return pelletsInSight * heuristicContext.Weights.LineOfSightPellets;
    }
}
