using MCTSo4.Models;

namespace MCTSo4.Algorithms.MCTS;

/// <summary>
/// Evaluates a game state using heuristic weights from BotParameters.
/// </summary>
public static class HeuristicFunction
{
    public static double Evaluate(MCTSGameState state, BotParameters parameters)
    {
        // TODO: This function needs to be refactored or removed as MCTSGameState.Evaluate now handles heuristics.
        // If kept, it needs access to botId, currentTickInSim, and maxSimDepth to call state.IsTerminal and state.Evaluate correctly.
        // Commenting out for now to resolve build errors.
        /*
        if (state.IsTerminal()) // This call is now invalid due to missing parameters
            return state.Evaluate(); // This call is now invalid due to missing parameters
        */
        return 0.0; // Return a default value
    }
}
