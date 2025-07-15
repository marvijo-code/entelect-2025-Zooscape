---
description: 'Automated Log Analysis & Test Creation: Analyze random log files, test bot decisions, and iteratively improve weights until tests pass.'
name: AutomatedLogAnalysisTestCreation
---

# Automated Log Analysis & Test Creation Workflow

## Purpose
Automate the analysis of random log files, create tests for interesting game states, and iteratively improve bot weights until tests pass while maintaining existing test compatibility.

## Workflow Steps

### 1. Select Random State Log File
- Navigate to `logs/` directory (ignore `2024/` subdirectory)
- Find `.json` files containing game states
- Select a random file with tick > 10 for analysis
- Tools: `list_dir`, `file_search`, `read_file`

### 2. Analyze Game State Context
- Use `tools/GameStateInspector/inspect-game-state.ps1` to analyze the selected state
- Command: `pwsh tools/GameStateInspector/inspect-game-state.ps1 -GameStateFile "path/to/state.json"`
- Extract key insights: animal position, nearby threats/opportunities, strategic context
- Tools: `run_terminal_cmd` with GameStateInspector

### 3. Run Bot Comparison Test
- Use `FunctionalTests/Controllers/TestController.cs` API endpoints
- Create test via POST `/api/test/create` with:
  - `TestName`: "AutoGen_[timestamp]_[tick]"
  - `CurrentGameState`: JSON from log file
  - `Bots`: ["ClingyHeuroBot", "ClingyHeuroBot2"]
  - `TestType`: "MultiBotArray"
- Run test via POST `/api/test/run/[testName]`
- Tools: `run_terminal_cmd` with curl/Invoke-RestMethod

### 4. Analyze Bot Decision Differences
- Compare actions between ClingyHeuroBot and ClingyHeuroBot2
- If actions differ significantly, proceed to weight adjustment
- If actions are similar, return to step 1 for new state
- Extract heuristic scores and reasoning from test results

### 5. Update Weights Iteratively
- Modify `Bots/ClingyHeuroBot2/heuristic-weights.json`
- Focus on weights that affect the differing decisions
- Increment/decrement weights by 0.1-0.2 based on analysis
- Tools: `read_file`, `search_replace` for weight updates

### 6. Validate Weight Changes
- Re-run the created test to verify improved performance
- Run existing tests: `dotnet test FunctionalTests/JsonDrivenTests.cs`
- If new test passes and existing tests remain passing, complete
- If tests fail, adjust weights and repeat from step 5
- Tools: `run_terminal_cmd` with dotnet test

### 7. Cleanup and Documentation
- Document the changes made and reasoning
- Clean up temporary test files if needed
- Log the successful weight adjustment

## Available Tools & Commands

### GameStateInspector
```powershell
pwsh tools/GameStateInspector/inspect-game-state.ps1 -GameStateFile "logs/game123/tick_045.json"
```

### Test API Commands
```powershell
# Start test API (if not running)
dotnet run --project FunctionalTests

# Create test
$body = @{
    TestName = "AutoGen_$(Get-Date -Format 'yyyyMMdd_HHmmss')_$tick"
    CurrentGameState = $gameStateJson
    Bots = @("ClingyHeuroBot", "ClingyHeuroBot2")
    TestType = "MultiBotArray"
} | ConvertTo-Json -Depth 10

Invoke-RestMethod -Uri "http://localhost:5000/api/test/create" -Method POST -Body $body -ContentType "application/json"

# Run test
Invoke-RestMethod -Uri "http://localhost:5000/api/test/run/$testName" -Method POST
```

### Weight File Path
- `Bots/ClingyHeuroBot2/heuristic-weights.json`

### Test Validation
```powershell
dotnet test FunctionalTests/JsonDrivenTests.cs --verbosity normal
```

## Success Criteria
- New test created from interesting game state
- Bot decisions improved through weight adjustment
- All existing JsonDrivenTests continue to pass
- Changes documented and reasoning clear

## AI Behavior
- Execute workflow autonomously without unnecessary questions
- Use PowerShell commands as specified in user rules
- Focus on actionable weight changes based on heuristic analysis
- Maintain existing test compatibility throughout process
- Provide concise status updates at each step
