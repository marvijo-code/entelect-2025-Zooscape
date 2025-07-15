---
description: 'Automated Log Analysis & Test Creation: Analyze random log files, test bot decisions, and iteratively improve weights until tests pass.'
name: AutomatedLogAnalysisTestCreation
---

# Automated Log Analysis & Test Creation Workflow

## Purpose
Automate the analysis of random log files, create tests for interesting game states, and iteratively improve StaticHeuro bot weights until tests pass while maintaining existing test compatibility.

## Test Infrastructure Overview
- **Consolidated Tests**: All 32 tests unified in `FunctionalTests/TestDefinitions/ConsolidatedTests.json`
- **StaticHeuro Bot**: Uses static weights from `heuristic-weights.json` (evolution disabled)
- **Validation**: Single test command validates all existing functionality
- **Enhanced GameStateInspector**: Now uses shared `BotUtils.IsTraversable` logic for accurate wall detection
- **Optimized**: Faster test loading and simplified maintenance

## Key Improvements Made
- **Shared Logic**: GameStateInspector now uses `Marvijo.Zooscape.Bots.Common.Utils.BotUtils.IsTraversable`
- **Accurate Wall Detection**: Legal move analysis correctly identifies walls and illegal moves
- **Enhanced Analysis**: Added legal move analysis to `StateAnalysis` class
- **Better Weight Targeting**: Focus on `LineOfSightPellets` and `ResourceClustering` for pellet-based decisions
- **Improved Test Validation**: All tests now pass with corrected acceptable actions

## Workflow Steps

### 1. Select Random State Log File
- Navigate to `logs/` directory (ignore `2024/` subdirectory)
- Find `.json` files containing game states
- Select a random file with tick > 10 for analysis
- **Tip**: Use recent log directories for more interesting game states
- Tools: `list_dir`, `file_search`, `read_file`

### 2. Analyze Game State Context
- Use **enhanced** `tools/GameStateInspector` (C# console app) for comprehensive analysis
- Command: `dotnet run --project tools/GameStateInspector -- "path/to/state.json" "BotNickname"`
- **NEW**: Inspector now includes legal move analysis using shared BotUtils logic
- Extract key insights: animal position, legal moves, pellet availability, strategic context
- **CRITICAL**: Pay attention to "Can Move [Direction]?" output - this shows legal moves
- **IMPORTANT**: NEVER read the JSON file directly - always use GameStateInspector for analysis
- Tools: `run_terminal_cmd` with GameStateInspector

```bash
# Example output includes:
# === LEGAL MOVE ANALYSIS ===
# Can Move Up? No      <- Wall blocks this direction
# Can Move Left? Yes   <- Legal move
# Can Move Right? Yes  <- Legal move  
# Can Move Down? No    <- Wall blocks this direction
```

### 3. Run StaticHeuro Bot Test
- **IMPORTANT**: Use `rv-run-visualizer.ps1` to start the API (runs on port 5008)
- Command: `powershell -ExecutionPolicy Bypass -File rv-run-visualizer.ps1`
- Create test via POST `/api/test/create` with correct structure:
  - `TestName`: "AutoGen_[timestamp]_[tick]"
  - `GameStateFile`: "tick_[number].json" (copy to FunctionalTests/GameStates/)
  - `CurrentGameState`: JSON from log file
  - `Bots`: ["StaticHeuro"]
  - `TestType`: "SingleBot"
- Run test via POST `/api/test/run/[testName]`
- **REQUIREMENT**: Test must initially fail (bot chooses suboptimal action)
- Tools: `run_terminal_cmd` with PowerShell Invoke-RestMethod

### 4. Analyze StaticHeuro Decision Quality
- Compare StaticHeuro's chosen action against GameStateInspector's legal move analysis
- **CRITICAL**: Verify chosen action is actually legal using inspector output
- **NEW INSIGHT**: Focus on scenarios where bot has limited legal options (2-3 moves)
- If bot's decision is reasonable, return to step 1 for new state
- **Key Heuristics to Analyze**:
  - `LineOfSightPellets`: Pellet visibility in each direction
  - `ResourceClustering`: Pellet grouping and accessibility
  - `TimeToCapture`: Immediate reward timing
  - `WallCollisionRisk`: Movement constraints (now accurately detected)

### 5. Update Weights Iteratively
- **Target File**: `Bots/StaticHeuro/heuristic-weights.json`
- **Proven Effective Changes**:
  - Increase `LineOfSightPellets`: 85.0 → 100.0 (for pellet visibility)
  - Increase `ResourceClustering`: 500.0 → 520.0 (for pellet grouping)
- **Strategy**: Increment weights by 10-20 points for meaningful impact
- **Focus Areas**:
  - Pellet-based decisions: `LineOfSightPellets`, `ResourceClustering`
  - Safety decisions: `WallCollisionRisk`, `ZookeeperAvoidance`
  - Immediate rewards: `TimeToCapture`, `ImmediatePelletReward`
- Tools: `read_file`, `search_replace` for weight updates

### 6. Validate Weight Changes
- Re-run the created test to verify improved performance
- **IMPORTANT**: Copy game state file to `FunctionalTests/GameStates/` directory
- Run consolidated tests: `dotnet test FunctionalTests/FunctionalTests.csproj --filter "FullyQualifiedName~JsonDrivenTests"`
- All 32 tests must pass (loaded from `ConsolidatedTests.json`)
- **Success Indicators**:
  - Log: `"Using static weights from heuristic-weights.json (evolution disabled)"`
  - Test: `"Test Summary: 32 passed, 0 failed"`
- Tools: `run_terminal_cmd` with dotnet test

### 7. Cleanup and Documentation
- **CRITICAL**: Update test definition with correct acceptable actions
- **NEW**: Use GameStateInspector legal move analysis to set acceptable actions
- Add new test to `ConsolidatedTests.json`:
  - Set `acceptableActions` to legal moves only (from inspector analysis)
  - Ensure description reflects actual game constraints
- Copy game state file to `FunctionalTests/GameStates/` if not already there
- Remove unused tick files to keep directory clean
- Document successful weight adjustments

## Enhanced Tools & Commands

### GameStateInspector (Enhanced)
```bash
# C# console app with shared BotUtils logic
dotnet run --project tools/GameStateInspector -- "logs/game123/tick_045.json" "StaticHeuro"

# Key output sections:
# - LEGAL MOVE ANALYSIS: Shows which moves are blocked by walls
# - PELLET ANALYSIS: Shows pellet availability in each direction
# - STRATEGIC CONTEXT: Position, threats, opportunities
```

### Test API Commands (Updated)
```powershell
# Start test API using visualizer script (port 5008)
powershell -ExecutionPolicy Bypass -File rv-run-visualizer.ps1

# Create test with correct structure
$gameStateJson = Get-Content 'logs/path/to/file.json' -Raw
$testName = "AutoGen_$(Get-Date -Format 'yyyyMMdd_HHmm')_$tick"
$gameState = $gameStateJson | ConvertFrom-Json

$body = @{
    TestName = $testName
    GameStateFile = "tick_$tick.json"  # Must match copied file name
    CurrentGameState = $gameState
    TestType = "SingleBot"
    Bots = @("StaticHeuro")
    Description = "Automated test for tick $tick - strategic context description"
    TickOverride = $false
} | ConvertTo-Json -Depth 10

# Create and run test
Invoke-RestMethod -Uri "http://localhost:5008/api/test/create" -Method POST -Body $body -ContentType "application/json"
Invoke-RestMethod -Uri "http://localhost:5008/api/test/run/$testName" -Method POST
```

### Weight Update Strategy
```json
// Effective weight changes from implementation:
{
  "LineOfSightPellets": 100.0,     // Increased from 85.0 for pellet visibility
  "ResourceClustering": 520.0,     // Increased from 500.0 for pellet grouping
  "TimeToCapture": -1.2,           // Penalty for delayed capture
  "WallCollisionRisk": -50.0,      // Penalty for wall collisions
  "ZookeeperAvoidance": 200.0      // Threat management
}
```

### Test Validation (Updated)
```bash
# Run all 32 consolidated tests with minimal output
dotnet test FunctionalTests/FunctionalTests.csproj --filter "FullyQualifiedName~JsonDrivenTests" --logger "console;verbosity=minimal"

# Alternative: Run with quiet verbosity (less output)
dotnet test FunctionalTests/FunctionalTests.csproj --filter "FullyQualifiedName~JsonDrivenTests" --verbosity quiet

# Success output should show:
# - "Test Summary: 32 passed, 0 failed"
# - "Using static weights from heuristic-weights.json (evolution disabled)"
# - All test names including your new "AutoGen_" test
# - "Passed!  - Failed:     0, Passed:    33, Skipped:     0, Total:    33"
```

## File Management
- **Game State Files**: Copy to `FunctionalTests/GameStates/` as `tick_[number].json`
- **Test Definitions**: Add to `FunctionalTests/TestDefinitions/ConsolidatedTests.json`
- **Weight Files**: `Bots/StaticHeuro/heuristic-weights.json`
- **Cleanup**: Remove unused tick files to maintain clean directory

## Common Pitfalls & Solutions

### 1. Illegal Move in Test Definition
- **Problem**: Test expects "Down" but Down is blocked by wall
- **Solution**: Use GameStateInspector legal move analysis to set correct acceptable actions
- **Fix**: Update `acceptableActions` to only include legal moves

### 2. API Port Issues
- **Problem**: Test creation fails with connection errors
- **Solution**: Use `rv-run-visualizer.ps1` script (runs on port 5008, not 5000)
- **Command**: `powershell -ExecutionPolicy Bypass -File rv-run-visualizer.ps1`

### 3. Missing Game State Files
- **Problem**: Test fails with "file not found" error
- **Solution**: Copy game state JSON to `FunctionalTests/GameStates/` directory
- **Command**: `Copy-Item 'logs/path/file.json' 'FunctionalTests/GameStates/tick_[number].json'`

### 4. Weight Changes Too Small
- **Problem**: Bot behavior doesn't change after weight adjustment
- **Solution**: Use larger increments (10-20 points) for meaningful impact
- **Focus**: `LineOfSightPellets` and `ResourceClustering` for pellet decisions

## Success Criteria
- New test created from game state **that initially fails**
- StaticHeuro bot decisions improved through targeted weight adjustment
- Test includes only legal moves as acceptable actions (verified by inspector)
- All 32 existing JsonDrivenTests continue to pass
- GameStateInspector uses shared BotUtils logic for accurate analysis
- Changes documented with clear reasoning

## AI Behavior Guidelines
- Execute workflow autonomously without unnecessary questions
- Use PowerShell commands as specified in user rules
- **ALWAYS verify legal moves** using GameStateInspector before setting acceptable actions
- Focus on actionable weight changes based on pellet availability and clustering
- **Use shared logic** - GameStateInspector now uses same traversability logic as bots
- Maintain existing test compatibility throughout process
- Provide concise status updates at each step
- Clean up unused files after successful implementation
