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

        // Calculate both consecutive and total linked pellets
        int consecutivePellets = CountPelletsInDirection(
            heuristicContext.MyNewPosition,
            action,
            pelletPositions
        );

        int totalLinkedPellets = CountTotalLinkedPellets(
            heuristicContext.MyNewPosition,
            action,
            pelletPositions
        );

        // Hybrid scoring: balance consecutive pellets with total linked pellets
        // Optimized based on game state analysis to better prioritize immediate pellet collection
        // and long pellet chains
        
        // Consecutive pellets provide immediate value and clear path
        decimal consecutiveScore = consecutivePellets * 0.2m;
        
        // Linked pellets provide long-term value for efficient collection
        decimal linkedScore = totalLinkedPellets * 1.0m;
        
        // Apply diminishing returns to very large linked pellet groups to avoid overvaluing
        // extremely large clusters that might lead the bot away from optimal paths
        if (totalLinkedPellets > 20)
        {
            linkedScore = 20 + (totalLinkedPellets - 20) * 0.5m;
        }
        
        // Immediate pellet bonus for the first step (if there's a pellet right in front)
        decimal immediateBonus = 0m;
        var firstStepPos = GetFirstStepPosition(heuristicContext.MyNewPosition, action);
        if (pelletPositions.Contains(firstStepPos))
        {
            immediateBonus = 10.0m; // Stronger bonus for immediate pellet
        }
        
        return (consecutiveScore + linkedScore + immediateBonus) * heuristicContext.Weights.LineOfSightPellets;
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

    private static (int x, int y) GetFirstStepPosition((int x, int y) startPos, BotAction direction)
    {
        return direction switch
        {
            BotAction.Up => (startPos.x, startPos.y - 1),
            BotAction.Down => (startPos.x, startPos.y + 1),
            BotAction.Left => (startPos.x - 1, startPos.y),
            BotAction.Right => (startPos.x + 1, startPos.y),
            _ => startPos // Default case, shouldn't happen with valid moves
        };
    }

    private static int CountTotalLinkedPellets((int x, int y) startPos, BotAction direction, HashSet<(int X, int Y)> pelletPositions)
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

        var visited = new HashSet<(int, int)>();
        var queue = new Queue<(int x, int y)>();
        
        // Start from the first step in the direction
        int startX = startPos.x + dx;
        int startY = startPos.y + dy;
        
        if (pelletPositions.Contains((startX, startY)))
        {
            queue.Enqueue((startX, startY));
            visited.Add((startX, startY));
        }
        
        int totalPellets = 0;
        
        // BFS to find all connected pellets in this general direction
        while (queue.Count > 0)
        {
            var (currentX, currentY) = queue.Dequeue();
            totalPellets++;
            
            // Check all 4 directions for connected pellets
            var neighbors = new[]
            {
                (currentX, currentY - 1), // Up
                (currentX, currentY + 1), // Down
                (currentX - 1, currentY), // Left
                (currentX + 1, currentY)  // Right
            };
            
            foreach (var (nx, ny) in neighbors)
            {
                if (!visited.Contains((nx, ny)) && pelletPositions.Contains((nx, ny)))
                {
                    // Prioritize pellets that are generally in the same direction
                    bool isInSameDirection = direction switch
                    {
                        BotAction.Up => ny <= startPos.y,
                        BotAction.Down => ny >= startPos.y,
                        BotAction.Left => nx <= startPos.x,
                        BotAction.Right => nx >= startPos.x,
                        _ => true
                    };
                    
                    if (isInSameDirection)
                    {
                        queue.Enqueue((nx, ny));
                        visited.Add((nx, ny));
                    }
                }
            }
        }
        
        return totalPellets;
    }
}
