# System Patterns

## System Build

- The system consists of a central game engine (likely Dockerized) and multiple bot processes.
- Bots are .NET applications.

## Key Decisions

- Bots communicate with the engine, presumably over a network connection (e.g., WebSockets or HTTP).

## Architecture

- Follows a client-server model where the engine is the server and bots are clients. 