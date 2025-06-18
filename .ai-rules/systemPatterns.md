# System Patterns

## System Build

- The system consists of a central game engine and multiple bot processes.
- Bots are developed as standalone executables that connect to the engine.

## Key Architectural Patterns

- **Client-Server Model:** The game engine is the server, and bots are clients that connect via SignalR.
- **Event-Driven Bot Logic:** The bot's main loop is passive, reacting to `On` message events from the SignalR connection (e.g., receiving a new `GameState`).
- **BitBoard for Game State:** The C++ `GameState` uses `BitBoard` representations for walls, pellets, and power-ups. This is efficient for collision detection and spatial queries but requires careful population from the source JSON.
- **Connection Retry Mechanism:** To handle race conditions where the bot starts before the server, a robust connection retry loop with exponential backoff or fixed delays is implemented in the bot's entry point.