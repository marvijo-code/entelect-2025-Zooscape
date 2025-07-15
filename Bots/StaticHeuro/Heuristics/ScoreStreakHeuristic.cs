using Marvijo.Zooscape.Bots.Common;
using Marvijo.Zooscape.Bots.Common.Enums;
using Marvijo.Zooscape.Bots.Common.Models;

namespace StaticHeuro.Heuristics;

public class ScoreStreakHeuristic : IHeuristic
{
    public string Name => "ScoreStreak";

    public decimal CalculateScore(IHeuristicContext heuristicContext)
    {
        var (newX, newY) = heuristicContext.MyNewPosition;
        var targetCell = heuristicContext.CurrentGameState.Cells.FirstOrDefault(c => c.X == newX && c.Y == newY);

        // Only apply a bonus if the move is to a cell with a regular pellet.
        if (targetCell != null && targetCell.Content == CellContent.Pellet)
        {
            // The raw score is the current score streak.
            // The weight for this heuristic will determine how valuable the streak is.
            return heuristicContext.CurrentAnimal.ScoreStreak * heuristicContext.Weights.ScoreStreakBonus;
        }

        return 0m;
    }
}
