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
        // Get current position from context
        var currentPos = (heuristicContext.CurrentAnimal.X, heuristicContext.CurrentAnimal.Y);
        var action = GetActionFromPositions(currentPos, heuristicContext.MyNewPosition);
        
        if (action == BotAction.UseItem)
        {
            return 0; // No pellets to count for item usage
        }

        // Count pellets in the direction of movement from current position
        int pelletsInDirection = CountPelletsInDirection(
            heuristicContext.CurrentGameState, 
            currentPos, 
            action
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

    private int CountPelletsInDirection(GameState gameState, (int x, int y) startPos, BotAction direction)
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

        // Count pellets in the direction until we hit a wall or boundary
        while (true)
        {
            var cell = gameState.Cells.FirstOrDefault(c => c.X == currentX && c.Y == currentY);
            
            // Stop if we hit a boundary or wall
            if (cell == null || cell.Content == CellContent.Wall)
                break;
                
            // Count pellets
            if (cell.Content == CellContent.Pellet)
                pelletsFound++;

            // Move to next position in the same direction
            currentX += dx;
            currentY += dy;
        }

        return pelletsFound;
    }
}
