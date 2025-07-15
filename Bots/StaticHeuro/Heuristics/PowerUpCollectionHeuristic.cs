using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace StaticHeuro.Heuristics;

public class PowerUpCollectionHeuristic : IHeuristic
{
    public string Name => "PowerUpCollection";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var (newX, newY) = heuristicContext.MyNewPosition;
        var targetCell = heuristicContext.CurrentGameState.Cells.FirstOrDefault(c => c.X == newX && c.Y == newY);

        if (targetCell == null)
        {
            return 0m;
        }

        switch (targetCell.Content)
        {
            case CellContent.PowerPellet:
            case CellContent.ChameleonCloak:
            case CellContent.Scavenger:
            case CellContent.BigMooseJuice:
                // Return a positive score to incentivize picking up any power-up.
                // This can be refined later to value different power-ups differently.
                return heuristicContext.Weights.PowerUpCollection;
            default:
                return 0m;
        }
    }
}
