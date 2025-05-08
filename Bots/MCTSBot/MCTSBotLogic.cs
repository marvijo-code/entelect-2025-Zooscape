using System;
using MCTSBot.Algorithms.MCTS;
using MCTSBot.Models;
using MCTSBot.Services;

namespace MCTSBot;

public class MCTSBotLogic
{
    private readonly MCTSAlgorithm _mctsAlgorithm;
    private readonly InstructionService _instructionService;
    private Guid _botId;
    private int _iterationsPerMove; // Configurable MCTS iterations

    public MCTSBotLogic(
        InstructionService instructionService,
        int iterationsPerMove = 1000,
        double explorationParameter = 1.414
    )
    {
        _instructionService = instructionService;
        _mctsAlgorithm = new MCTSAlgorithm(explorationParameter);
        _iterationsPerMove = iterationsPerMove;
    }

    public void SetBotId(Guid botId)
    {
        _botId = botId;
        _instructionService.SetBotId(botId); // Also inform the instruction service
        Console.WriteLine($"MCTSBotLogic initialized with BotId: {botId}");
    }

    /// <summary>
    /// Processes the incoming game state and decides on the next move.
    /// </summary>
    /// <param name="engineGameState">The game state received from the engine.</param>
    /// <returns>The command to be sent to the game engine.</returns>
    public EngineBotCommand ProcessState(EngineGameState engineGameState)
    {
        if (engineGameState == null)
        {
            Console.WriteLine("Received null engineGameState, doing nothing.");
            // Return a DoNothing command or handle as appropriate
            return _instructionService.CreateBotCommand(GameAction.DoNothing);
        }

        Console.WriteLine($"Processing game state for Tick: {engineGameState.Tick}");

        // 1. Translate EngineGameState to MCTSGameState
        MCTSGameState mctsCurrentState = _instructionService.TranslateToMCTSState(engineGameState);

        // 2. Run MCTS algorithm to find the best move
        // You might want to add a time limit here as well, not just iterations
        Console.WriteLine($"Running MCTS with {_iterationsPerMove} iterations...");
        GameAction bestAction = _mctsAlgorithm.FindBestMove(mctsCurrentState, _iterationsPerMove);
        Console.WriteLine($"MCTS decided on action: {bestAction}");

        // 3. Translate the chosen MCTS GameAction to an EngineBotCommand
        EngineBotCommand command = _instructionService.CreateBotCommand(bestAction);

        return command;
    }
}
