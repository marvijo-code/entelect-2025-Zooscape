# Important File Paths

This file tracks important and frequently accessed file paths within the project.

## AdvancedMCTSBot (C++)

- `Bot_Implementation`: Bots/AdvancedMCTSBot/Bot.cpp
- `GameState_Implementation`: Bots/AdvancedMCTSBot/GameState.cpp
- `MCTS_Engine`: Bots/AdvancedMCTSBot/MCTSEngine.cpp
- `MCTS_Node`: Bots/AdvancedMCTSBot/MCTSNode.cpp
- `CMake_Build_Script`: Bots/AdvancedMCTSBot/CMakeLists.txt

## Bot Projects

- `MCTSo4_CSProject`: Bots/MCTSo4/MCTSo4.csproj
- `MCTSo4_Program`: Bots/MCTSo4/Program.cs
- `ClingyHeuroBot2_CSProject`: Bots/ClingyHeuroBot2/ClingyHeuroBot2.csproj
- `ClingyHeuroBot2_Service`: Bots/ClingyHeuroBot2/Services/HeuroBotService.cs
- `ClingyHeuroBotExp_CSProject`: Bots/ClingyHeuroBotExp/ClingyHeuroBotExp.csproj
- `ClingyHeuroBot_CSProject`: Bots/ClingyHeuroBot/ClingyHeuroBot.csproj

## Functional Testing

- `FunctionalTests_Project`: FunctionalTests/FunctionalTests.csproj
- `StandardBotTests`: FunctionalTests/StandardBotTests.cs
- `BotFactory`: FunctionalTests/Services/BotFactory.cs
- `GameStates_Directory`: FunctionalTests/GameStates/
- `GameState12_JSON`: FunctionalTests/GameStates/12.json

## Game Analysis Tools (Enhanced)

- `GameInspector_Project`: tools/GameStateInspector/GameStateInspector.csproj
- `GameInspector_Program`: tools/GameStateInspector/Program.cs ✅ **ENHANCED with --analyze-move**
- `GameInspector_README`: tools/GameStateInspector/README.md
- `GameInspector_Script`: tools/GameStateInspector/inspect-game-state.ps1
- `GameInspector_Directory`: tools/GameStateInspector/

## Scripts

- `RunAllScript`: ra-run-all.ps1
- `ZooscapeRunner_Script`: ra-local-run-manager-win.ps1

## React Visualizer Components

- `VisualizerApp`: visualizer-2d/src/App.jsx
- `VisualizerGrid`: visualizer-2d/src/components/Grid.jsx
- `VisualizerGameSelector`: visualizer-2d/src/components/GameSelector.jsx
- `VisualizerAppCSS`: visualizer-2d/src/App.css
- `VisualizerGameSelectorCSS`: visualizer-2d/src/styles/GameSelector.css

## Debugging Context (ACTIVE)
- `Bots/ClingyHeuroBot2/Heuristics/LineOfSightPelletsHeuristic.cs` - **BROKEN HEURISTIC** with debug logging
- `FunctionalTests/StandardBotTests.cs` - Test definitions (2 tests failing)
- `FunctionalTests/bin/Debug/net8.0/GameStates/162.json` - ChaseMorePelletGroups test data
- `FunctionalTests/bin/Debug/net8.0/GameStates/34.json` - ChaseImmediatePellet test data
- `tools/GameStateInspector/Program.cs` - Working analysis tool for ground truth

## Core Application Structure
- `engine/` - Core game engine and domain logic
- `Bots/` - Bot implementations and strategies
- `FunctionalTests/` - Test suite for bot behavior validation
- `tools/` - Development and analysis tools

## Bot Development
- `Bots/ClingyHeuroBot2/` - Main bot being debugged
- `Bots/ClingyHeuroBot2/Services/HeuroBotService.cs` - Bot service implementation
- `Bots/ClingyHeuroBot2/Heuristics/` - Individual heuristic implementations
- `Marvijo.Zooscape.Bots.Common/` - Shared bot interfaces and models

## Testing Infrastructure
- `FunctionalTests/StandardBotTests.cs` - Standard bot behavior tests
- `FunctionalTests/Services/BotFactory.cs` - Bot instantiation for tests
- `FunctionalTests/BotTestsBase.cs` - Base test functionality
- `FunctionalTests/bin/Debug/net8.0/GameStates/` - Test game state JSON files

## Analysis Tools
- `tools/GameStateInspector/` - **ENHANCED DEBUG TOOL** for game state analysis
- `tools/GameStateInspector/Program.cs` - Main inspector implementation ✅ **WITH --analyze-move**
- `tools/GameStateInspector/README.md` - Tool documentation
- `tools/GameStateInspector/inspect-game-state.ps1` - PowerShell wrapper

### Game Inspector Usage Examples:
```bash
# List all bots in a game state
dotnet run --project tools/GameStateInspector -- gamestate.json

# Analyze current bot position
dotnet run --project tools/GameStateInspector -- gamestate.json "BotName"

# Analyze what happens after a move (NEW FEATURE)
dotnet run --project tools/GameStateInspector -- gamestate.json "BotName" --analyze-move Up
dotnet run --project tools/GameStateInspector -- gamestate.json "BotName" --analyze-move Left
```

## Configuration & Documentation
- `.ai-rules/activeContext.md` - **CURRENT DEBUG SESSION** context
- `.ai-rules/systemPatterns.md` - Debugging patterns and methodologies
- `docs/` - Project documentation
- `engine/Rules.md` - Game rules and mechanics

## Build & Deployment
- `solutions/` - Solution files for different configurations
- `build.bat` - Build script
- `dependencies.txt` - Project dependencies

## Key Interfaces
- `Marvijo.Zooscape.Bots.Common/IBot.cs` - Bot interface
- `Marvijo.Zooscape.Bots.Common/IHeuristic.cs` - Heuristic interface
- `Marvijo.Zooscape.Bots.Common/HeuristicContext.cs` - Context for heuristic calculations