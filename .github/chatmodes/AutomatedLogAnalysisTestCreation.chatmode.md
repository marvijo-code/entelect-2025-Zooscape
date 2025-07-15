---
description: 'Automated Log Analysis & Test Creation: Analyze random log files, test bot decisions, and iteratively improve weights until tests pass.'
name: AutomatedLogAnalysisTestCreation
---

# Automated Log Analysis & Test Creation Workflow

## Purpose
Automate the analysis of random log files, create tests for interesting game states, and iteratively improve StaticHeuro bot weights until tests pass while maintaining existing test compatibility.

## Test Infrastructure Overview
- **Consolidated Tests**: All 31 tests unified in `FunctionalTests/TestDefinitions/ConsolidatedTests.json`
- **StaticHeuro Bot**: Uses static weights from `heuristic-weights.json` (evolution disabled)
- **Validation**: Single test command validates all existing functionality
- **Optimized**: Faster test loading and simplified maintenance

## Workflow Steps

### 1. Select Random State Log File
- Navigate to `logs/` directory (ignore `2024/` subdirectory)
- Find `.json` files containing game states
- Select a random file with tick > 10 for analysis
- Tools: `list_dir`, `file_search`, `read_file`

### 2. Analyze Game State Context
- Use `tools/GameStateInspector/inspect-game-state.ps1` to analyze the selected state
- Command: `pwsh tools/GameStateInspector/inspect-game-state.ps1 -GameStateFile "path/to/state.json" -BotNickname "StaticHeuro"`
- Extract key insights: animal position, nearby threats/opportunities, strategic context
- Tools: `run_terminal_cmd` with GameStateInspector

### 3. Run StaticHeuro Bot Test
- Use `FunctionalTests/Controllers/TestController.cs` API endpoints
- Create test via POST `/api/test/create` with:
  - `TestName`: "AutoGen_[timestamp]_[tick]"
  - `CurrentGameState`: JSON from log file
  - `Bots`: ["StaticHeuro"]
  - `TestType`: "SingleBot"
- Run test via POST `/api/test/run/[testName]`
- **REQUIREMENT**: Test must initially fail (bot chooses suboptimal action)
- Tools: `run_terminal_cmd` with curl/Invoke-RestMethod

### 4. Analyze StaticHeuro Decision Quality
- Evaluate StaticHeuro's chosen action against game state analysis
- Compare bot's action choice with strategic insights from GameStateInspector
- **CRITICAL**: Only proceed if bot's decision is suboptimal (test initially fails)
- If bot's decision is reasonable, return to step 1 for new state
- Extract heuristic scores and reasoning from test results
- Identify 1-2 optimal actions based on weight score analysis

### 5. Update Weights Iteratively
- Modify `Bots/StaticHeuro/heuristic-weights.json`
- Focus on weights that affect the suboptimal decision
- Increment/decrement weights by 0.1-0.2 based on analysis
- **Target**: Make 1-2 optimal actions score higher than current choice
- Tools: `read_file`, `search_replace` for weight updates

### Weight Score Analysis Guide
- Compare total scores for each possible action (Up, Down, Left, Right)
- Identify actions with significant score gaps (>10 points recommended)
- Focus on heuristics with largest impact on score differences:
  - `ResourceClustering`: Pellet accessibility and grouping
  - `TimeToCapture`: Immediate reward timing
  - `WallCollisionRisk`: Safety and movement constraints
  - `ZookeeperAvoidance`: Threat management

### 6. Validate Weight Changes
- Re-run the created test to verify improved performance
- Run consolidated tests: `dotnet test FunctionalTests/JsonDrivenTests.cs`
- All 31 tests must pass (loaded from `ConsolidatedTests.json`)
- If new test passes and existing tests remain passing, complete
- If tests fail, adjust weights and repeat from step 5
- Tools: `run_terminal_cmd` with dotnet test

### 7. Cleanup and Documentation
- Document the changes made and reasoning
- Add new test to `ConsolidatedTests.json` if validation passes:
  - Include 1-2 `acceptableActions` based on weight score analysis
  - Ensure test initially failed before weight adjustment
- Clean up temporary test files if needed
- Log the successful weight adjustment

## Available Tools & Commands

### GameStateInspector
```powershell
# PowerShell script method
pwsh tools/GameStateInspector/inspect-game-state.ps1 -GameStateFile "logs/game123/tick_045.json" -BotNickname "StaticHeuro"

# C# console app method (alternative)
dotnet run --project tools/GameStateInspector -- "logs/game123/tick_045.json" "StaticHeuro"
```

### Test API Commands
```powershell
# Start test API (if not running)
dotnet run --project FunctionalTests

# Create test (must initially fail)
$body = @{
    TestName = "AutoGen_$(Get-Date -Format 'yyyyMMdd_HHmmss')_$tick"
    CurrentGameState = $gameStateJson
    Bots = @("StaticHeuro")
    TestType = "SingleBot"
    AcceptableActions = @("Up", "Right")  # 1-2 actions based on weight analysis
} | ConvertTo-Json -Depth 10

Invoke-RestMethod -Uri "http://localhost:5000/api/test/create" -Method POST -Body $body -ContentType "application/json"

# Run test
Invoke-RestMethod -Uri "http://localhost:5000/api/test/run/$testName" -Method POST
```

### Weight File Path
- `Bots/StaticHeuro/heuristic-weights.json`

### Test Infrastructure
- **Consolidated Tests**: All 31 tests stored in `FunctionalTests/TestDefinitions/ConsolidatedTests.json`
- **Test Loader**: `TestDefinitionLoader.cs` loads from single consolidated file
- **Test Types**: SingleBot, MultiBotArray, GameStateLoad, TickOverride
- **StaticHeuro Integration**: Uses static weights (evolution disabled)

### Test Selection Criteria
- **Initial Failure Requirement**: Only create tests for game states where StaticHeuro chooses suboptimal actions
- **Acceptable Actions Limit**: Define 1-2 optimal actions based on:
  - Highest scoring heuristic combinations
  - Strategic game state analysis
  - Clear performance differences (>10 point score gaps recommended)
- **Skip Scenarios**: If bot already chooses optimal action, find different game state

### Test Validation
```powershell
# Run all 31 consolidated tests
dotnet test FunctionalTests/JsonDrivenTests.cs --verbosity normal

# Filter to specific test class
dotnet test FunctionalTests/FunctionalTests.csproj --filter "FullyQualifiedName~JsonDrivenTests"

# All tests loaded from consolidated file
# Location: FunctionalTests/TestDefinitions/ConsolidatedTests.json
```

## Success Criteria
- New test created from interesting game state **that initially fails**
- StaticHeuro bot decisions improved through weight adjustment
- Test includes 1-2 acceptable actions based on weight score analysis
- All 31 existing JsonDrivenTests continue to pass (from ConsolidatedTests.json)
- Changes documented and reasoning clear

## Success Indicators
- Log message: `"Using static weights from heuristic-weights.json (evolution disabled)"`
- Test output: `"Loaded 31 test definitions from ConsolidatedTests.json"`
- Test result: `"Test Summary: 31 passed, 0 failed"`
- Weight application: StaticHeuro uses updated weights without evolution interference

## AI Behavior
- Execute workflow autonomously without unnecessary questions
- Use PowerShell commands as specified in user rules
- **ONLY select game states where StaticHeuro initially fails**
- Focus on actionable weight changes based on heuristic analysis
- **Limit acceptable actions to 1-2 based on weight score analysis**
- Maintain existing test compatibility throughout process
- Provide concise status updates at each step
