# ADR: Hybrid MCTS-Driven Adaptive Agent Implementation - 2025-05-08

**Status:** Proposed

**Context:**
Based on the strategy outlined in `docs/strategy-deep-research-06-MAY-2025.md`, we will implement Strategy 12 (Hybrid MCTS-Driven Adaptive Agent) in C#, mirroring the structure and conventions of `Bots/BasicBot`.

**Implementation Plan:**

1. Create new bot folder `Bots/MCTSo4` with subdirectories:
   - `Algorithms/MCTS`
   - `Models`
   - `Enums`
   - `Services`
   - `Tests`

2. Initialize C# project:
   - Copy `Bots/BasicBot/BasicBot.csproj` to `Bots/MCTSo4/MCTSo4.csproj`
   - Update `<AssemblyName>`, `<RootNamespace>` to `MCTSo4`
   - Ensure `<OutputType>Exe`, `<TargetFramework>net8.0` and SignalR package references are present

3. Models (`Bots/MCTSo4/Models`):
   - `MCTSGameState.cs`: full game state for MCTS
   - `Move.cs`: enum or struct of possible actions
   - `GameResult.cs`: simulation outcome

4. Algorithms (`Bots/MCTSo4/Algorithms/MCTS`):
   - `Node.cs`: tree node with UCT calculation
   - `MCTSAlgorithm.cs`: methods for Selection, Expansion, Simulation, Backpropagation, and `FindBestMove`

5. Services (`Bots/MCTSo4/Services`):
   - `EngineParser.cs`: parse engine JSON/state into `MCTSGameState`
   - `CommandFormatter.cs`: convert `Move` to engine command string

6. Bot integration:
   - `MCTSo4Logic.cs`: glue between engine, MCTSAlgorithm, and Services
   - `Program.cs`: entry point similar to BasicBot

7. Tests (`Bots/MCTSo4/Tests`):
   - Unit tests for `Node` and `MCTSAlgorithm`
   - Unit tests for `MCTSGameState`, `Move`, `GameResult`
   - Integration test simulating a short playout

8. Add project to solution and verify build:
   - Update solution file or use `dotnet sln add`
   - Ensure `dotnet build` and `dotnet test` pass locally

**Timestamp:** 2025-05-08T13:03:33+02:00
