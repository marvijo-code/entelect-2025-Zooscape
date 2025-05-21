# DeepMCTS Bot - Optimized Monte Carlo Implementation

This bot implements an optimized Monte Carlo Tree Search approach for the Zooscape game, focusing on computational efficiency and correct game rule behavior.

## Key Features from Prompt

* **Bitwise Pellet Tracking:** Represents the game grid's pellet layout as a bitset for fast operations.
* **Minimal Memory Allocation:** Avoids creating new objects inside the simulation loop. Structs and primitive fields are used for critical game state.
* **Efficient Data Structures:** Fixed two-dimensional `bool` array for walls (`walls[x,y]`) for O(1) access. Precomputed neighbor offsets.
* **Numeric Representations & Bit Ops:** Bit-shifting and masking for 2D position to index conversion and pellet checks.
* **Optimized Zookeeper Movement:** Zookeeper retargets based on game rules (e.g., every 20 ticks), using BFS for pathfinding, reducing costly calls.
* **Early Termination & Horizon Limiting:** Simulations stop after a fixed horizon (e.g., 50 ticks) or on terminal events like capture.
* **Heuristics in Random Rollouts:** Lightweight heuristics to guide random simulations (e.g., avoiding immediate 180° reversals).
* **Respawning on Capture:** Bot correctly respawns to its cage upon capture, and zookeeper behavior reflects this.
* **Valid Moves Only:** Simulation checks boundaries and walls, ensuring only legal moves are made.

## Implementation Details (based on provided code)

### Key Components
- `BotService.cs`: Contains the main bot logic (`ProcessState`) and the static `MonteCarlo` class.
- `MonteCarlo` (static class): Handles game state initialization (`InitGameState`), finding the best move (`FindBestMove`), and running simulations (`SimulatePlayout`).

## Performance Notes (Targeted, based on prompt)
- Aims for significantly more iterations than typical MCTS due to optimizations (e.g., 2.5x to 3x, targeting 2400-2800 iterations in 150ms).

This README will be updated as the bot is further developed and tested. 