using System;
using MCTSBot.Enums;
using MCTSBot.Models;

namespace MCTSBot.Services;

public class InstructionService
{
    private Guid _botId; // Set upon registration

    public void SetBotId(Guid botId)
    {
        _botId = botId;
    }

    /// <summary>
    /// Translates the game state received from the engine into the MCTS-specific game state.
    /// This is a crucial step and will require detailed mapping based on actual game data.
    /// </summary>
    public MCTSGameState TranslateToMCTSState(EngineGameState engineState)
    {
        // TODO: Implement translation logic from EngineGameState to MCTSGameState
        throw new NotImplementedException("TranslateToMCTSState is not implemented.");
    }

    /// <summary>
    /// Translates the MCTS-chosen GameAction into an EngineBotCommand.
    /// </summary>
    public EngineBotCommand CreateBotCommand(GameAction mctsAction)
    {
        EngineBotAction engineAction;
        switch (mctsAction)
        {
            case GameAction.MoveUp:
                engineAction = EngineBotAction.Up;
                break;
            case GameAction.MoveDown:
                engineAction = EngineBotAction.Down;
                break;
            case GameAction.MoveLeft:
                engineAction = EngineBotAction.Left;
                break;
            case GameAction.MoveRight:
                engineAction = EngineBotAction.Right;
                break;
            case GameAction.DoNothing:
            default:
                engineAction = EngineBotAction.DoNothing;
                break;
        }
        // SequenceNumber or other fields might be needed.
        // For now, assuming BotId is set during registration and action is primary.
        Console.WriteLine(
            $"Creating EngineBotCommand for MCTSAction: {mctsAction} -> EngineAction: {engineAction}"
        );
        return new EngineBotCommand(_botId, engineAction);
    }
}
