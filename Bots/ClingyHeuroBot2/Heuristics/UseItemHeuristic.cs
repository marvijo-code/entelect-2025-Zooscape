using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace ClingyHeuroBot2.Heuristics;

public class UseItemHeuristic : IHeuristic
{
    public string Name => "UseItem";

    public decimal CalculateRawScore(IHeuristicContext heuristicContext)
    {
        // This heuristic only applies if the move being considered is UseItem.
        if (heuristicContext.CurrentMove != BotAction.UseItem)
        {
            return 0m;
        }

        // If the animal isn't holding a power-up, there's no score for using one.
        if (heuristicContext.CurrentAnimal.HeldPowerUp == null)
        {
            return 0m;
        }

        // Check which power-up the animal is holding and score it.
        switch (heuristicContext.CurrentAnimal.HeldPowerUp)
        {
            case PowerUpType.ChameleonCloak:
            case PowerUpType.Scavenger:
            case PowerUpType.BigMooseJuice:
                return 1.0m; // Encourage using these items

            case PowerUpType.PowerPellet: // PowerPellets are not "used", they are consumed on pickup
            default:
                return 0m; // No score for trying to use other items
        }
    }
}
