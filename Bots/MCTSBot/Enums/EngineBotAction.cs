namespace MCTSBot.Enums;

/// <summary>
/// Represents the actions that can be sent to the game engine.
/// This should match the actions expected by the game runner.
/// </summary>
public enum EngineBotAction
{
    // Assuming values align with BasicBot's BotAction enum if it starts from a certain value
    // Or simply define them sequentially if not.
    // For this example, let's assume a direct mapping from internal GameAction for simplicity of translation later.
    Up = 0, // Corresponds to GameAction.MoveUp
    Down = 1, // Corresponds to GameAction.MoveDown
    Left = 2, // Corresponds to GameAction.MoveLeft
    Right = 3, // Corresponds to GameAction.MoveRight
    DoNothing = 4, // Corresponds to GameAction.DoNothing
    // Add other actions like UsePowerUp if the engine expects them as distinct commands
}
