# System Patterns

## System Build

- The system consists of a central game engine and multiple bot processes.
- Bots are developed as standalone executables that connect to the engine.

## Key Architectural Patterns

- **Client-Server Model:** The game engine is the server, and bots are clients that connect via SignalR.
- **Event-Driven Bot Logic:** The bot's main loop is passive, reacting to `On` message events from the SignalR connection (e.g., receiving a new `GameState`).
- **BitBoard for Game State:** The C++ `GameState` uses `BitBoard` representations for walls, pellets, and power-ups. This is efficient for collision detection and spatial queries but requires careful population from the source JSON.
- **Connection Retry Mechanism:** To handle race conditions where the bot starts before the server, a robust connection retry loop with exponential backoff or fixed delays is implemented in the bot's entry point.
- **SignalR C++ Client to C# Server Command Serialization:**
    - **Context:** When a C++ bot sends commands to the Zooscape C# game server (e.g., to the "BotCommand" hub method).
    - **Payload Structure:**
        1.  Send a single argument to the hub method.
        2.  This argument must be a `signalr::value` representing a map (serializes to a JSON object).
        3.  The map must contain a key named `"Action"` (PascalCase).
        4.  The value for `"Action"` is the integer representation of the `BotAction` enum, cast to `double` for `signalr::value` creation.
        5.  Example: `std::map<std::string, signalr::value> cmdMap; cmdMap["Action"] = signalr::value(static_cast<double>(action_enum_value));`
    - **Rationale:** Mismatched payload (wrong keys, types, or argument count) leads to server-side deserialization failure, often resulting in "Invalid command (0)" errors as the server processes a default-initialized command object. Casting numbers to `double` for `signalr::value` is crucial.