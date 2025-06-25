# Active Context: Functional Testing & Game State Analysis

## Current Task
Implementing and debugging functional tests for bot behavior verification, specifically testing ClingyHeuroBot2 with GameState 12.json to ensure it moves Up as expected.

## Recent Changes
1. **Functional Test Implementation:**
   - Added new test `GameState12ClingyHeuroBot2MustMoveUp` in `FunctionalTests/StandardBotTests.cs`
   - Fixed compilation errors in `FunctionalTests/Services/BotFactory.cs` by commenting out problematic DeepMCTS and MCTSo4 references
   - Fixed IndexOutOfRangeException bug in `Bots/ClingyHeuroBot2/Services/HeuroBotService.cs` where list insertion was failing due to insufficient elements

2. **Game State Inspector Tool - Now Official:**
   - **Moved to official location:** `tools/GameStateInspector/`
   - Created comprehensive documentation with `README.md`
   - Added PowerShell wrapper script `inspect-game-state.ps1` for easier usage
   - Tool analyzes JSON game state files and provides detailed information about:
     - Bot position and score
     - Immediate pellet availability in all directions
     - Pellets within 3-step range
     - Current quadrant location
     - Nearest zookeeper position and distance
   - Successfully used to analyze 12.json for ClingyHeuroBot2

## Current Test Results
- **Test Status:** ❌ FAILING - ClingyHeuroBot2 chooses Down instead of Up
- **Expected:** Up movement
- **Actual:** Down movement (score: 1012.4014 vs Up score: 314.7143)
- **Root Cause:** LineOfSightPellets heuristic heavily favors Down direction (800.00 points) vs Up (100.00 points)

## Game Inspector Usage (Official Tool)
**Location:** `tools/GameStateInspector/`
**Direct Command:** `dotnet run -- <path-to-json-file> <bot-nickname>`
**Wrapper Script:** `.\inspect-game-state.ps1 -GameStateFile <file> -BotNickname <bot>`
**Example:** `dotnet run -- ../../FunctionalTests/GameStates/12.json ClingyHeuroBot2`

**Output Provides:**
- Bot position (X, Y coordinates)
- Current score
- Immediate pellet detection (Up/Down/Left/Right)
- Pellet count within 3-step range for each direction
- Current quadrant (Top-Left, Top-Right, Bottom-Left, Bottom-Right)
- Nearest zookeeper location and distance

## Next Steps
1. **Analyze Game State 12 Further:** Use Game Inspector to understand why Down direction has significantly more pellets visible than Up
2. **Review Heuristic Weights:** The LineOfSightPellets heuristic may need adjustment or the test expectation may be incorrect
3. **Validate Test Expectation:** Confirm whether the bot SHOULD move Up in this scenario or if the test needs updating
4. **Consider Game Rules:** Review GameRules.md to understand optimal movement strategy for this game state

## Key Files Modified
- `FunctionalTests/StandardBotTests.cs` - Added GameState12 test
- `FunctionalTests/Services/BotFactory.cs` - Fixed compilation errors
- `Bots/ClingyHeuroBot2/Services/HeuroBotService.cs` - Fixed IndexOutOfRangeException
- `tools/GameStateInspector/Program.cs` - Official game state analysis tool
- `tools/GameStateInspector/README.md` - Comprehensive tool documentation
- `tools/GameStateInspector/inspect-game-state.ps1` - PowerShell wrapper script

## Technical Notes
- Game Inspector tool successfully parses JSON game states and provides actionable analysis
- ClingyHeuroBot2 logging shows detailed heuristic scoring breakdown
- Fixed multiple bounds checking issues in bot logging code
- All compilation errors resolved, functional tests now execute successfully
- Tool is now officially supported with full documentation and helper scripts