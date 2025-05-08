namespace MCTSBot.Models;

/// <summary>
/// Represents a possible action a bot can take.
/// </summary>
public enum GameAction
{
    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    DoNothing,
    // Potentially add UsePowerUp actions here later, e.g., UsePowerUp1, UsePowerUp2
    // Or have a separate mechanism for power-ups if they are more complex than simple actions.
}
