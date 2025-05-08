# ADR: MCTS Bot Implementation Plan - 2025-05-08

**Status:** Proposed

**Context:**

The project requires the implementation of a high-performing bot for the Zooscape challenge, based on the Monte Carlo Tree Search (MCTS) algorithm. The strategy is detailed in `docs/strategy-deep-research-06-MAY-2025.md`. The new bot should follow a similar project structure and instruction format to `Bots/BasicBot`.

**Decision:**

We will implement an MCTS bot with the following components and structure:

1.  **New Bot Project (`Bots/MCTSBot`):**
    *   A new C# project, `MCTSBot.csproj`, adapted from `BasicBot.csproj`.
    *   Directory structure mirroring `BasicBot` (e.g., `Algorithms`, `Models`, `Services`, `Enums`).

2.  **MCTS Core Logic (`Bots/MCTSBot/Algorithms/MCTS`):**
    *   `Node.cs`:
        *   `GameState`: Simplified representation of the game state for MCTS.
        *   `Parent`: Reference to the parent node.
        *   `Children`: List of child nodes.
        *   `Move`: The game move that led to this node.
        *   `Visits`: Count of visits during MCTS simulations.
        *   `Wins`: Count of wins from simulations passing through this node.
        *   `PlayerIndex`: Index of the player whose turn it is in this state.
        *   `IsTerminal`: Boolean indicating if this node represents a game-ending state.
        *   `GetUntriedMoves()`: Returns moves not yet expanded from this node.
        *   `AddChild(Move move, GameState state)`: Creates and adds a child node.
        *   `Update(GameResult result)`: Updates wins/visits based on simulation outcome for the current player.
        *   `UCTValue(double explorationParameter)`: Calculates the UCT value for node selection.
    *   `MCTSAlgorithm.cs`:
        *   `SelectPromisingNode(Node rootNode)`: Selects a node for expansion (UCT).
        *   `ExpandNode(Node node)`: Expands a selected node by adding a new child.
        *   `SimulateRandomPlayout(Node node)`: Runs a simulation (random or semi-random policy) from the expanded node to a terminal state.
        *   `Backpropagate(Node node, GameResult result)`: Propagates the simulation result back up the tree.
        *   `FindBestMove(GameState initialGameState, int iterations, double explorationParameter)`: Orchestrates the MCTS process (selection, expansion, simulation, backpropagation) for a given number of iterations and returns the best move.

3.  **Game State and Action Modeling (`Bots/MCTSBot/Models`):**
    *   `MCTSGameState.cs`: Represents the game state relevant for MCTS. This includes:
        *   Player bot's position, score, current power-ups.
        *   Positions and states of other player bots.
        *   Positions and states/predicted movements of zookeepers.
        *   Locations of pellets.
        *   Locations and types of power-ups.
        *   Map layout (walls, traversable areas).
        *   Current player index.
        *   `GetPossibleMoves()`: Returns a list of valid moves from the current state.
        *   `ApplyMove(Move move)`: Returns a new `MCTSGameState` resulting from applying a move.
        *   `IsTerminal()`: Checks if the game state is terminal.
        *   `GetGameResult()`: Determines the outcome/score if the state is terminal.
    *   `Move.cs` (or `GameAction.cs`): Represents possible actions (e.g., `UP`, `DOWN`, `LEFT`, `RIGHT`, `USE_POWERUP_X`, `DO_NOTHING`). Likely an enum or a struct/class.
    *   `GameResult.cs`: Represents the outcome of a simulation (e.g., win/loss for current player, or a score vector for multiplayer scenarios).

4.  **Bot Integration (`Bots/MCTSBot`):**
    *   `MCTSBotLogic.cs` (or similar, replacing the core logic in `BasicBot/Program.cs`):
        *   Receives the game state from the game engine.
        *   Translates the engine's game state into the `MCTSGameState` format.
        *   Initializes and runs the `MCTSAlgorithm.FindBestMove()` method.
        *   Translates the chosen `Move` back into the specific command string format expected by the game engine (matching `BasicBot`'s output format).
    *   `Program.cs`: Main entry point for the executable. Sets up and runs `MCTSBotLogic`.

5.  **Instruction Formatting (`Bots/MCTSBot/Services` or similar):**
    *   A dedicated class/service responsible for parsing incoming game state strings and formatting outgoing command strings, ensuring compatibility with the game engine and `BasicBot`'s conventions.

6.  **Testing:**
    *   Unit tests for all core MCTS components (`Node.cs`, `MCTSAlgorithm.cs`).
    *   Unit tests for `MCTSGameState.cs` methods (`GetPossibleMoves`, `ApplyMove`, `IsTerminal`, `GetGameResult`).
    *   Integration tests for the bot logic, potentially using mock game engine interactions.

**Consequences:**

*   A new, more sophisticated bot will be created, potentially offering superior performance.
*   The codebase will increase in complexity compared to `BasicBot`.
*   Significant effort will be required for implementation and thorough testing, especially for the MCTS algorithm and game state simulation logic.
*   The bot's performance will heavily depend on the accuracy of the game state representation, the quality of the simulation playout policy, and the number of MCTS iterations achievable within the game's time limits per turn.

**Next Steps:**

1.  Create the directory structure for `Bots/MCTSBot` (already done for `Bots/MCTSBot` root).
2.  Create subdirectories: `Algorithms`, `Models`, `Services`, `Enums` inside `Bots/MCTSBot`.
3.  Copy and adapt `BasicBot.csproj` to `Bots/MCTSBot/MCTSBot.csproj`.
4.  Begin implementation of `Models` (MCTSGameState, Move, GameResult).
5.  Implement `Algorithms/MCTS` (Node, MCTSAlgorithm).
6.  Implement bot integration logic and instruction formatting.
7.  Write unit and integration tests.
