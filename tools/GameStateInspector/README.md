# Game State Inspector

A C# console application for analyzing JSON game state files to understand bot decision-making context and debug bot behavior.

## Purpose

The Game State Inspector helps developers:
- Analyze specific game states to understand why bots make certain decisions
- Debug failing functional tests by examining the game context
- Validate test expectations against actual game state conditions
- Understand heuristic scoring context before adjusting bot weights

## Usage

### Basic Command
```bash
cd tools/GameStateInspector
dotnet run -- <path-to-json-file> <bot-nickname>
```

### Examples
```bash
# Analyze game state 12 for ClingyHeuroBot2
dotnet run -- ../../FunctionalTests/GameStates/12.json ClingyHeuroBot2

# Analyze game state 162 for any bot
dotnet run -- ../../FunctionalTests/GameStates/162.json SomeBot
```

## Output Analysis

The tool provides detailed information about:

### Bot Context
- **Position**: Current X, Y coordinates of the specified bot
- **Score**: Current game score for the bot
- **Quadrant**: Which quadrant of the map the bot is currently in (Top-Left, Top-Right, Bottom-Left, Bottom-Right)

### Movement Analysis
- **Immediate Pellets**: Boolean flags for pellet availability in each direction (Up, Down, Left, Right)
- **3-Step Range**: Count of pellets reachable within 3 moves in each direction
- **Strategic Context**: Distance and position of nearest zookeeper

### Sample Output
```
=== GAME STATE ANALYSIS ===
Bot: ClingyHeuroBot2
Position: (15, 34)
Score: 914

Immediate Pellet Availability:
- Pellet Up? True
- Pellet Left? False  
- Pellet Right? False
- Pellet Down? False

Pellets in 3-step range:
- Up: 3 pellets
- Left: 1 pellet
- Right: 0 pellets  
- Down: 0 pellets

Current Quadrant: Bottom-Left
Nearest Zookeeper: (24, 19) at distance 24
```

## Integration with Testing Workflow

### Recommended Debugging Process
1. **Run failing functional test** to see actual vs expected behavior
2. **Use Game Inspector** to analyze the game state context
3. **Review bot's heuristic scoring output** from test logs
4. **Make informed decisions** about weight adjustments or test expectation validation

### Example Workflow
```bash
# 1. Run the failing test
dotnet test FunctionalTests/FunctionalTests.csproj --filter "GameState12ClingyHeuroBot2MustMoveUp"

# 2. Analyze the game state
cd tools/GameStateInspector
dotnet run -- ../../FunctionalTests/GameStates/12.json ClingyHeuroBot2

# 3. Compare with bot's heuristic output to understand decision-making
```

## Technical Details

### Supported Game Elements
- **Cells**: Walls, pellets, power-ups, escape zones
- **Animals**: All bot positions and metadata
- **Grid Analysis**: Automatic grid dimension detection
- **Spatial Queries**: Distance calculations and directional analysis

### Error Handling
- Validates JSON file existence and format
- Handles missing bot nicknames gracefully
- Provides clear error messages for invalid inputs

## File Structure
- `Program.cs`: Main application logic and game state analysis
- `GameStateInspector.csproj`: .NET project configuration
- `README.md`: This documentation file

## Dependencies
- .NET 8.0 or later
- System.Text.Json for JSON parsing
- No external NuGet packages required 