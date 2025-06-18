# Active Context

**Primary Goal:** Fix the `AdvancedMCTSBot`'s core logic to enable MCTS expansion and intelligent action selection.

**Recent Work Summary:**

The bot was consistently choosing action 0 because its MCTS engine never expanded the root node. This was traced back to a series of critical bugs in connection handling, JSON parsing, and game state logic, which have now been resolved.

1.  **Connection Stability:** Implemented a connection retry loop in `Bot::run()` to handle race conditions where the bot starts before the game server is ready.
2.  **JSON Game State Parsing:**
    - **Key Casing:** Discovered the SignalR C++ client converts all incoming JSON keys to `lowercase`. Corrected all parsing logic in `Bot.cpp` to use lowercase keys (e.g., `"cells"`, `"animals"`), which fixed the bot's inability to find itself in the `animals` list.
    - **Grid Dimensions:** The server does not send `gridWidth` or `gridHeight`. Implemented a two-pass system in `Bot.cpp`: the first pass calculates the grid dimensions by finding the max X/Y from the `cells` array, and the second pass populates the bitboards. This fixed the empty `pelletBoard` issue.
3.  **Game Logic Correction:**
    - **Obstacle Detection:** The `GameState::isTraversable` method was checking against an uninitialized `grid` member instead of the populated `wallBoard`. Corrected the function to check `!wallBoard.get(x, y)`, which finally allowed the bot to find legal moves.
4.  **Diagnostics:** Added and subsequently cleaned up extensive `fmt::println` logging to trace the flow of data from JSON to the MCTS engine, which was crucial for identifying the root causes.

**Next Immediate Step:**

- Run the bot to verify that all fixes work together, resulting in a fully functional MCTS bot that expands the game tree and chooses varied, intelligent actions.