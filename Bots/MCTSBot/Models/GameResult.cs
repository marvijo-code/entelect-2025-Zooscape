namespace MCTSBot.Models;

/// <summary>
/// Represents the result of a game simulation for MCTS.
/// </summary>
public class GameResult
{
    /// <summary>
    /// Score or win/loss status for the player who made the move leading to the simulation.
    /// For simplicity, let's use a double. Positive for win/good outcome, negative for loss/bad outcome.
    /// This can be refined to support multiplayer scores (e.g., a dictionary or array of scores).
    /// </summary>
    public double Score { get; set; }

    // Could also include properties like:
    // public bool IsWin { get; set; }
    // public int WinnerPlayerIndex { get; set; }

    public GameResult(double score)
    {
        Score = score;
    }
}
