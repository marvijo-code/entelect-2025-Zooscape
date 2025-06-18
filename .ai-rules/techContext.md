# Tech Context

## Technologies

- **C++ (17):** Used for the `AdvancedMCTSBot`.
- **CMake:** Used for the C++ build system.
- **Microsoft SignalR C++ Client:** Handles real-time communication with the game server.
- **`fmt` library:** Used for structured and formatted logging.
- **Standard Library:** Extensive use of `<thread>`, `<chrono>`, `<optional>`, and STL containers.
- **PowerShell:** Used for build and run automation scripts.

## Dev Setup

- Visual Studio 2022 with C++ and CMake support.
- PowerShell for running scripts.

## Constraints & Key Learnings

- **Time Limit:** Bots have a 150ms time limit per tick to make a move.
- **SignalR:** The SignalR C++ client library is used for real-time communication with the game server.
- **fmt:** The `fmt` library is used for structured and formatted logging.
- **JSON Key Casing:** The SignalR C++ client library **converts all incoming JSON keys to `lowercase`**. This is a critical constraint that must be handled during game state parsing. All JSON accessors in the C++ code must use lowercase keys (e.g., `"cells"`, `"animals"`) regardless of the casing in the source JSON.