#pragma warning disable SKEXP0110
using System;
using System.Collections.Generic;
using System.Linq;
using ClingyHeuroBot2.Heuristics;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace ClingyHeuroBot2.Heuristics;

public static class Heuristics
{
    public static (int x, int y) ApplyMove(int x, int y, BotAction m) =>
        m switch
        {
            BotAction.Up => (x, y - 1),
            BotAction.Down => (x, y + 1),
            BotAction.Left => (x - 1, y),
            BotAction.Right => (x + 1, y),
            _ => (x, y),
        };

    public static int ManhattanDistance(int x1, int y1, int x2, int y2) =>
        Math.Abs(x1 - x2) + Math.Abs(y1 - y2);

    public static bool IsTraversable(GameState state, int x, int y)
    {
        // Check if the cell exists and is not a wall
        var cell = state.Cells.FirstOrDefault(c => c.X == x && c.Y == y);
        return cell != null && cell.Content != CellContent.Wall;
    }
}
