# Bot Analysis Documentation

This folder contains logically organized documentation files extracted from the original `.github/chatmodes/AutomatedLogAnalysisTestCreation.chatmode.md` workflow.  Each file focuses on a specific aspect of bot analysis, debugging, or test-creation so you can jump directly to the topic you need without scrolling through a single monolithic document. Always improve the StaticHeuro bot!!!

## 🚨 CRITICAL WORKFLOW RULES 🚨

### Test Creation Rules
- **NEVER manually create or edit test JSON files** - Always use `create_test.ps1` script
- **NEVER manually edit `ConsolidatedTests.json`** - The `create_test.ps1` script handles this automatically
- **Always use the proper script parameters** when creating tests via `create_test.ps1`

### Heuristic Analysis Rules
- **NEVER analyze or modify heuristic weights without first seeing the bot's actual output** after running against the scenario state JSON file
- **Always run the bot against the specific game state** to see detailed heuristic scoring before making changes
- **Use GameStateInspector or API endpoints** to get actual heuristic breakdowns, not assumptions

### Test Execution Rules
- **To run all JSON-driven tests**: `dotnet test FunctionalTests --filter "ExecuteAllJsonDefinedTests"`
- **Use specific test names** or API endpoints to run individual tests**: `Invoke-RestMethod -Method POST -Uri "http://localhost:5008/api/test/run/TestName"`
- **Always stop the API first** (`./start-api.ps1 -Stop`) before any `dotnet build` or `dotnet test` command to prevent DLL file locks.
- **Always restart the API** (`./start-api.ps1 -Force`) after code or weight changes and after tests complete

### create_test.ps1 Usage
- **Purpose**: Automatically adds tests to `ConsolidatedTests.json` - never edit this file manually
- **Required Parameters**: `-GameStateFile`, `-TestName`
- **Example**: `./create_test.ps1 -GameStateFile "290.json" -TestName "StaticHeuro_AvoidCapture_290" -BotNicknameInState "StaticHeuro" -AcceptableActions "3" -Description "Bot must avoid capture by moving Left"`
- **Action Codes**: 1=Up, 2=Down, 3=Left, 4=Right
- **The script handles**: JSON formatting, API calls, and ConsolidatedTests.json updates automatically

### Proper Bot Analysis Workflow
1. **Copy game state to FunctionalTests/GameStates/**: `cp "logs/match/tick.json" "FunctionalTests/GameStates/tick.json"`
2. **Analyze bot's actual decision**: Use GameStateInspector or API to see what the bot actually chooses and why
3. **Get detailed heuristic breakdown**: `cd tools/GameStateInspector && dotnet run -- ../../FunctionalTests/GameStates/tick.json StaticHeuro`
4. **Review heuristic scoring**: Look at the actual scores, not assumptions about what they should be
5. **Create test via script**: `./create_test.ps1 -GameStateFile "tick.json" -TestName "TestName" -AcceptableActions "X"`
6. **Make informed changes**: Only adjust heuristics/weights after seeing actual bot output
7. **Restart API**: `./start-api.ps1 -Force`
8. **Verify fix**: Run the specific test to confirm the change works

- after analysis don't ask questions like, "Would you like me to proceed with implementing these fixes?", always proceed to implement weight fixes to make the bot better

- ALWAYS RUN ALL tests after adding tests to make sure that all tests still pass

## File Index

| File | Purpose |
|------|---------|
| `critical-workflow.md` | MUST-READ first.  Contains the non-negotiable critical workflow instructions and the golden rule to always restart the API. |
| `lessons-staticheuro-pellet.md` | Detailed case study of the StaticHeuro pellet-collection bug and how it was debugged & fixed. |
| `automated-weight-analysis.md` | Guides and tooling for automated heuristic-weight optimisation and long-term path-decision analysis. |
| `scenario-based-tests.md` | Step-by-step instructions for creating scenario-focused functional tests (zookeeper proximity, capture avoidance, etc.). |
| `troubleshooting.md` | Quick reference table of common problems & fixes plus consolidated key lessons learned. |
| `available-tools.md` | Central list of every CLI tool and helper script with purpose and usage examples. |

> **Tip:**  Always start with `critical-workflow.md` to ensure you follow the required debugging process.

---

## Tools Overview

Below is a quick reference of the primary CLI tools and helper scripts you will use when analysing bots, debugging, and creating tests.  All paths are relative to the project **root** unless noted otherwise.

| Tool / Script | Location | Purpose | Example |
|---------------|----------|---------|---------|
| **GameStateInspector** | `tools/GameStateInspector/` | Analyse a single JSON game-state file, list legal moves, and profile heuristic timing. | `cd tools/GameStateInspector && dotnet run -- ../../FunctionalTests/GameStates/12.json StaticHeuro` |
| **CaptureAnalysis** | `tools/CaptureAnalysis/` | Scan a match-log folder for captures; flags *AVOIDABLE* vs *UNAVOIDABLE*. | `dotnet run --project tools/CaptureAnalysis -- "logs/20250717_match" StaticHeuro` |
| **FindPelletIgnoreStates** | `tools/FindPelletIgnoreStates/` | Identify game states where the bot ignored nearby pellets. | `dotnet run --project tools/FindPelletIgnoreStates -- "logs/20250717_match" StaticHeuro` |
| **inspect-game-state.ps1** | project root | Wrapper that runs GameStateInspector from anywhere. | `.\inspect-game-state.ps1 -GameStateFile "FunctionalTests/GameStates/953.json" -BotNickname "StaticHeuro"` |
| **find_close_zookeeper_state.ps1** | project root | PowerShell helper to locate states where a zookeeper is adjacent to the bot. | `.\find_close_zookeeper_state.ps1 -LogDirectory "logs/20250717_match" -BotNickname "StaticHeuro"` |
| **create_test.ps1** | project root | Generates a functional-test JSON stub from a game-state file. | `.\create_test.ps1 -GameStateFile "FunctionalTests/GameStates/953.json" -BotNickname "StaticHeuro" -AcceptableActions "Right"` |
| **generate-path-error-tests.ps1** | project root | Auto-generates tests for detected path-decision errors. | `.\generate-path-error-tests.ps1 -LogDirectory "logs/20250717_match" -BotNickname "StaticHeuro" -ErrorType "cluster_abandonment"` |
| **start-api.ps1** | project root | API lifecycle management (stop, start, restart) | `./start-api.ps1 -Force` |
| **stop-api.ps1** | project root | Convenience wrapper around `./start-api.ps1 -Stop` to prevent file locks before test runs | `./stop-api.ps1` |

### Running a Single Functional Test

You can execute an individual test case in **two** ways:

1. **Via the API** (preferred for quick loops):
   ```powershell
   Invoke-RestMethod -Method POST -Uri "http://localhost:5008/api/test/run/StaticHeuro_AdjacentPellet_953"
   ```

2. **Via dotnet test filter:**
   ```powershell
   dotnet test --filter "FullyQualifiedName~StaticHeuro_AdjacentPellet_953"
   ```

Remember to **restart the API** (`start-api.ps1 -Force`) after any code or weight change before rerunning the test.

### 🎯 Enhanced Test Endpoint with Heuristic Scores

The test endpoint now returns **detailed heuristic scores** for supported bots (StaticHeuro, ClingyHeuroBot2). This provides comprehensive insight into bot decision-making:

#### Response Structure
```json
{
  "testName": "StaticHeuro_StuckBot_51",
  "success": true,
  "botResults": [
    {
      "botType": "StaticHeuro",
      "action": "Right",
      "success": true,
      "performanceMetrics": {
        "ActionScores": {
          "Up": 245.67,
          "Down": -78.95,
          "Left": 156.23,
          "Right": 318.20
        },
        "DetailedScores": [
          {
            "Move": "Right",
            "TotalScore": 318.20,
            "DetailedLogLines": [
              "    CaptureAvoidanceHeuristic        : Raw=  2.5000, Weight= 10.0000, Contribution= 25.0000, NewScore= 25.0000",
              "    LineOfSightPelletsHeuristic     : Raw=  9.0000, Weight= 50.0000, Contribution=450.0000, NewScore=475.0000",
              "    WallCollisionRiskHeuristic      : Raw= -0.2000, Weight=  1.0000, Contribution= -0.2000, NewScore=474.8000"
            ]
          }
        ]
      }
    }
  ]
}
```

#### Key Features
- **ActionScores**: Shows the final score for each possible move (Up, Down, Left, Right)
- **DetailedScores**: Provides complete heuristic breakdown for each move including:
  - Individual heuristic contributions
  - Raw values, weights, and calculated contributions
  - Running total scores
- **Automatic Detection**: Uses `GetActionWithDetailedScores` when available, falls back to `GetAction` for unsupported bots
- **LogHeuristicScores**: Automatically enabled for test runs to capture detailed scoring

#### Debugging Workflow
1. **Run Test**: `POST http://localhost:5008/api/test/run/<testname>`
2. **Analyze Scores**: Review `ActionScores` to see why bot chose specific action
3. **Deep Dive**: Examine `DetailedScores` to understand individual heuristic contributions
4. **Adjust Weights**: Modify `heuristic-weights.json` based on analysis
5. **Restart API**: `start-api.ps1 -Force` to apply changes
6. **Re-test**: Verify improved behavior

#### Example Usage
```powershell
# Get detailed scores for a specific test
$result = Invoke-RestMethod -Method POST -Uri "http://localhost:5008/api/test/run/StaticHeuro_StuckBot_51"

# Extract action scores
$actionScores = $result.botResults[0].performanceMetrics.ActionScores
Write-Host "Bot chose: $($result.botResults[0].action) with score: $($actionScores[$result.botResults[0].action])"

# Show all action alternatives
$actionScores | Format-Table
```

This enhancement makes it significantly easier to debug bot decision-making and optimize heuristic weights without needing to parse console logs.
