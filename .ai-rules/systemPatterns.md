# System Patterns & Debugging Methodologies

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

## Testing Patterns

- **Functional Testing:** Tests use specific game state JSON files to verify bot behavior in known scenarios
- **Test Structure:** Tests in `FunctionalTests/StandardBotTests.cs` follow pattern:
  - Load specific game state JSON file
  - Target specific bot by nickname
  - Assert expected action (Up/Down/Left/Right)
  - Use `TestBotAction` method for execution

## Game State Analysis Tools (Official)

- **Game Inspector Tool:** Official C# console application at `tools/GameStateInspector/`
- **Purpose:** Analyze JSON game state files to understand bot decision-making context
- **Usage Patterns:**
  ```bash
  # Direct usage
  cd tools/GameStateInspector
  dotnet run -- <path-to-json-file> <bot-nickname>
  



  ```
- **Enhanced Output Analysis:**
  - Bot position and current score
  - Immediate pellet availability (Up/Down/Left/Right boolean)
  - Pellet count within 3-step range for each direction
  - Consecutive pellets in each direction (line-of-sight analysis)
  - Pellet distribution by quadrant
  - Current quadrant location
  - Nearest zookeeper position and distance
- **Integration Features:**
  - Comprehensive README documentation

  - Error handling for missing files and invalid bot names
  - Automatic path resolution for game state files
- **Debugging Workflow:**
  1. Run failing functional test to see actual vs expected behavior
  2. Use Game Inspector to analyze the game state context
  3. Review bot's heuristic scoring output from test logs
  4. Compare Inspector output with bot's decision-making logic
  5. Adjust weights or validate test expectations based on comprehensive analysis

## Error Handling Patterns

- **Bounds Checking:** Always validate list/array indices before insertion/access
- **Example Fix:** `int insertIndex = Math.Max(0, currentDetailedLog.Count - 2);` before list insertion
- **Null Reference Prevention:** Check for null/empty collections before operations
- **Tool Validation:** Game Inspector validates file existence, JSON format, and bot presence before analysis

## Heuristic Debugging Pattern (Critical)
**When fixing bot heuristics that affect multiple tests:**

1. **Isolate the Problem:**
   - Run single failing test first to understand the core issue
   - Use Game State Inspector to get ground truth about game state
   - Compare heuristic output with expected behavior

2. **Fix Incrementally:**
   - Make minimal changes to fix the primary issue
   - Run ALL tests immediately after any heuristic change
   - If new tests fail, the fix likely has fundamental logic errors

3. **Debug with Logging:**
   - Add debug logging to heuristic calculations
   - Extract debug messages from test output (they exist but need careful parsing)
   - Compare logged values with Game State Inspector analysis

4. **Verify Direction Mapping:**
   - Coordinate systems can be confusing (Y-axis up vs down)
   - Direction mapping (Up/Down/Left/Right) may not match expectations
   - Always verify with known game state positions

## Game State Analysis Pattern
**Use Game State Inspector for ground truth:**
- Tool location: `tools/GameStateInspector/`
- Command: `dotnet run -- <json-file> <bot-nickname>`
- Provides authoritative pellet counts, positions, and game state analysis
- Always compare heuristic calculations against inspector results

## Test-Driven Debugging
**When multiple tests fail after a fix:**
1. **Don't assume the fix is correct** - it likely introduced new bugs
2. **Analyze the pattern** - what's common across failing tests?
3. **Use existing working tools** (Game State Inspector) for verification
4. **Fix the root cause**, not just the symptoms

## Functional Test Patterns
**Test file locations:**
- Tests: `FunctionalTests/StandardBotTests.cs`
- Game states: `FunctionalTests/bin/Debug/net8.0/GameStates/`
- Bot factory: `FunctionalTests/Services/BotFactory.cs`

**Common test patterns:**
- Tests expect specific bot actions based on game state
- Game states are JSON files with full game board data
- Bot behavior is determined by heuristic scoring
- Multiple bots may be tested with same game state

## Heuristic Implementation Patterns
**File pattern:** `Bots/{BotName}/Heuristics/{HeuristicName}Heuristic.cs`
**Interface:** `IHeuristic` with `CalculateScore(IHeuristicContext context)`
**Context provides:**
- `CurrentAnimal` - bot's current position and state
- `MyNewPosition` - destination position for the move being evaluated
- `CurrentGameState` - full game state including all cells and animals
- `Weights` - configured weights for this heuristic

## Debugging Memory Bank Updates
**Always update Memory Bank when:**
- Debugging session reveals complex multi-step issues
- Multiple tests are failing due to interconnected problems
- Debug evidence accumulates that needs to be preserved
- Fresh session would benefit from detailed context

**Memory Bank should include:**
- Current problem state and evidence
- Debug commands to resume efficiently
- Key insights and patterns discovered
- Files modified and their current state