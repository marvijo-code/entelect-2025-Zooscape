using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace StaticHeuro.Heuristics;

public class PowerUpCollectionHeuristic : IHeuristic
{
    public string Name => "PowerUpCollection";

        public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var myBot = heuristicContext.CurrentAnimal;
        var (newX, newY) = heuristicContext.MyNewPosition;

        var powerUpCells = heuristicContext.CurrentGameState.Cells
            .Where(c =>
                c.Content == CellContent.PowerPellet ||
                c.Content == CellContent.ChameleonCloak ||
                c.Content == CellContent.Scavenger ||
                c.Content == CellContent.BigMooseJuice)
            .ToList();

        if (!powerUpCells.Any())
        {
            return 0m;
        }

        // Find the nearest power-up
        var nearestPowerUp = powerUpCells
            .OrderBy(p => Math.Abs(p.X - myBot.X) + Math.Abs(p.Y - myBot.Y))
            .First();

        // If the move lands on the power-up, give a large, immediate bonus
        if (newX == nearestPowerUp.X && newY == nearestPowerUp.Y)
        {
            return heuristicContext.Weights.PowerUpCollection;
        }

        // Calculate distance to the nearest power-up from current and new positions
        var currentDistance = Math.Abs(heuristicContext.CurrentAnimal.X - nearestPowerUp.X) + Math.Abs(heuristicContext.CurrentAnimal.Y - nearestPowerUp.Y);
        var newDistance = Math.Abs(newX - nearestPowerUp.X) + Math.Abs(newY - nearestPowerUp.Y);

        // Reward moves that get closer to the power-up
        if (newDistance < currentDistance)
        {
            // The score is inversely proportional to the distance: closer is better.
            return (1.0m / (newDistance + 1)) * heuristicContext.Weights.PowerUpCollection;
        }

        return 0m;
    }
}
