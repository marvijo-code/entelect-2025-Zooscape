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

- **Time Limit:** Bots have a 200ms time limit per tick to make a move (previously 150ms).
- **SignalR JSON Key Casing (C++ Client):** The SignalR C++ client library **converts all incoming JSON keys to `lowercase`**. This is a critical constraint that must be handled during game state parsing. All JSON accessors in the C++ code must use lowercase keys (e.g., `"cells"`, `"animals"`) regardless of the casing in the source JSON.
- **SignalR Command Serialization (C++ Client to C# Server):**
    - To send commands (e.g., to a "BotCommand" hub method) from a C++ client to a C# SignalR server:
        1.  Send a single argument to the hub method.
        2.  This argument must be a `signalr::value` representing a map (serializes to a JSON object).
        3.  The map must contain a key named `"Action"` (PascalCase).
        4.  The value for `"Action"` is the integer representation of the `BotAction` enum, cast to `double` for `signalr::value` creation.
        5.  Example: `std::map<std::string, signalr::value> cmdMap; cmdMap["Action"] = signalr::value(static_cast<double>(action_enum_value));`
    - Incorrect payload structure will likely result in the server receiving a default-initialized command object.