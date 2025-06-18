# Product Context: AdvancedMCTSBot

## Purpose

The primary focus is the development of `AdvancedMCTSBot`, a C++ bot designed to compete in the Zooscape game environment. The project serves as a practical application and testbed for implementing and debugging a sophisticated AI using a multi-threaded Monte Carlo Tree Search (MCTS) algorithm.

## Problems Solved

This effort addresses the complexities of real-time game AI, including:
- Robust communication with a game server (SignalR).
- Correctly parsing and interpreting complex, dynamically changing game state from JSON.
- Implementing a performant MCTS engine that can make decisions under a tight time constraint (currently 200ms, previously 150ms).
- Debugging subtle logic errors in game state representation (e.g., bitboards) and AI algorithms.

## How it Works

The game engine sends game state updates via a SignalR hub. The `AdvancedMCTSBot` receives this data, parses the JSON into a C++ `GameState` object, and uses its MCTS engine to simulate thousands of game outcomes to determine the best possible action. The chosen action is then sent back to the server.