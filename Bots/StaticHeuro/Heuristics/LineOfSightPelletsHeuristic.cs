#pragma warning disable SKEXP0110
using System.Linq;
using System.Collections.Generic;
using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;
using Serilog;

namespace StaticHeuro.Heuristics;

public class LineOfSightPelletsHeuristic : IHeuristic
{
    public string Name => "LineOfSightPellets";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        // Determine the direction we are moving in based on current vs next position.
        var currentPos = (heuristicContext.CurrentAnimal.X, heuristicContext.CurrentAnimal.Y);
        var action = GetActionFromPositions(currentPos, heuristicContext.MyNewPosition);

        if (action == BotAction.UseItem)
        {
            return 0m; // Not a movement action – nothing to evaluate.
        }

        // Build a hash-set of pellet coordinates for O(1) look-ups.
        var pelletPositions = heuristicContext.CurrentGameState.Cells
            .Where(c => c.Content == CellContent.Pellet)
            .Select(c => (c.X, c.Y))
            .ToHashSet();

        if (pelletPositions.Count == 0)
        {
            return 0m; // No pellets on the board.
        }

        int pelletsInDirection = CountPelletsInDirection(
            heuristicContext.MyNewPosition,
            action,
            pelletPositions
        );

        return pelletsInDirection * heuristicContext.Weights.LineOfSightPellets;
    }

    private BotAction GetActionFromPositions((int x, int y) current, (int x, int y) next)
    {
        int dx = next.x - current.x;
        int dy = next.y - current.y;

        return (dx, dy) switch
        {
            (0, -1) => BotAction.Up,
            (0, 1) => BotAction.Down,
            (-1, 0) => BotAction.Left,
            (1, 0) => BotAction.Right,
            _ => BotAction.UseItem // Default fallback
        };
    }

    private static int CountPelletsInDirection((int x, int y) startPos, BotAction direction, HashSet<(int X, int Y)> pelletPositions)
    {
        int dx = 0, dy = 0;
        
        switch (direction)
        {
            case BotAction.Up:
                dy = -1;
                break;
            case BotAction.Down:
                dy = 1;
                break;
            case BotAction.Left:
                dx = -1;
                break;
            case BotAction.Right:
                dx = 1;
                break;
            default:
                return 0;
        }

        int pelletsFound = 0;
        int currentX = startPos.x + dx;
        int currentY = startPos.y + dy;

        // Continue stepping along the ray until we hit a cell that is not a pellet
        while (pelletPositions.Contains((currentX, currentY)))
        {
            pelletsFound++;
            currentX += dx;
            currentY += dy;
        }

        return pelletsFound;
    }
}
