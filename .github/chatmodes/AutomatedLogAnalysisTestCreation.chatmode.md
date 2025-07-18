---
description: 'Automated Log Analysis & Test Creation: Analyze game logs, create, and run functional tests via API to iteratively improve bot heuristics.'
name: AutomatedLogAnalysisTestCreation
---

# Automated Log Analysis & Test Creation Workflow

## 🚨 CRITICAL: READ THIS ENTIRE FILE FIRST 🚨

> [!IMPORTANT]
> **BEFORE STARTING ANY DEBUGGING, TESTING, OR LOG ANALYSIS WORK:**
> 
> **YOU MUST READ THIS ENTIRE FILE FROM START TO FINISH**
> 
> This workflow has been battle-tested through real debugging scenarios like the StaticHeuro bot capture avoidance fix. Following this complete workflow prevents inefficient debugging, missed critical steps, and interface compatibility issues.
> 
> **This is not optional - it's mandatory for all bot debugging and testing tasks.**

## 1. The Golden Rule: Always Restart the API

> [!IMPORTANT]
> The `FunctionalTests` API server compiles and loads bot logic on startup. If you make **any code changes** to a bot's heuristics, services, or any related files, you **MUST** restart the API. Otherwise, your changes will not take effect, and you will be testing against stale code.

**Use this command to force a rebuild and restart:**
```powershell
# Run from the project root
.\start-api.ps1 -Force
```

## 2. Lessons Learned: StaticHeuro Pellet Collection Debug (2025-07-17)

### Key File Paths for Bot Debugging

**Critical Files for StaticHeuro Bot:**
- **Main Bot Logic**: `c:\dev\2025-Zooscape\Bots\StaticHeuroBot\Program.cs`
- **Heuristic Weights**: `c:\dev\2025-Zooscape\Bots\StaticHeuroBot\heuristic-weights.json`
- **Bot Service**: `c:\dev\2025-Zooscape\Bots\StaticHeuroBot\Services\HeuroBotService.cs`
- **Functional Tests**: `c:\dev\2025-Zooscape\FunctionalTests\TestDefinitions\ConsolidatedTests.json`
- **Game States**: `c:\dev\2025-Zooscape\FunctionalTests\GameStates\*.json`
- **Test Framework**: `c:\dev\2025-Zooscape\FunctionalTests\JsonDrivenTests.cs`
- **Test Loader**: `c:\dev\2025-Zooscape\FunctionalTests\Services\TestDefinitionLoader.cs`

**Heuristic Implementation Files:**
- **Base Directory**: `c:\dev\2025-Zooscape\Marvijo.Zooscape.Bots.Common\Heuristics\`
- **Key Heuristics**: `PelletEfficiencyHeuristic.cs`, `WallCollisionRiskHeuristic.cs`, `CaptureAvoidanceHeuristic.cs`

### Debugging Workflow for Adjacent Pellet Issues

**Step 1: Identify the Problem**
```powershell
# Run functional tests to see current failures
cd c:\dev\2025-Zooscape\FunctionalTests
dotnet test --filter "FullyQualifiedName~StaticHeuro"
```

**Step 2: Analyze Specific Game State**
```powershell
# Use GameStateInspector to understand bot's decision-making
cd c:\dev\2025-Zooscape\tools\GameStateInspector
dotnet run -- "..\..\FunctionalTests\GameStates\953.json" "StaticHeuro"
```

**Step 3: Check Heuristic Balance**
- Look for **WallCollisionRisk** penalties outweighing **PelletEfficiency** rewards
- **ImmediatePelletBonus** should be high enough (500+) to override other penalties
- **PelletEfficiencyHeuristic** should return immediate bonus when landing on pellets

**Step 4: Create Targeted Test**
- Add test definition to `ConsolidatedTests.json`
- Use specific game state where bot has adjacent pellet
- Verify expected action matches actual bot decision

**Step 5: Common Fixes**
- **Scope Issues**: Move variable declarations to broader scope in `Program.cs`
- **Bot ID Issues**: Remove duplicate `SetBotId` methods in bot services
- **Heuristic Balance**: Increase pellet rewards, decrease wall collision penalties
- **Compilation**: Fix ambiguous references (e.g., `System.IO.File` vs `File`)

### Performance Tips

**Fast Test Execution:**
```powershell
# Run only StaticHeuro tests
dotnet test --filter "FullyQualifiedName~StaticHeuro" --logger "console;verbosity=minimal"

# Run specific test by name (if framework supports it)
dotnet test --filter "TestName~StaticHeuro_AdjacentPellet_953"
```

**Quick Heuristic Weight Changes:**
- Edit `heuristic-weights.json` directly
- **Always restart API** after changes: `.\.\start-api.ps1 -Force`
- Test changes immediately with targeted functional test

**Debugging Compilation Issues:**
- Check for duplicate method definitions
- Verify `using` statements are complete
- Look for variable scope issues in bot's main loop

## 3. Automated Weight Adjustment & Path Decision Analysis

### 2.1 Automated Heuristic Weight Optimization

**Goal**: Automatically detect when heuristic weights need adjustment based on bot performance patterns and failed tests.

#### Step 2.1.1: Performance Pattern Detection

- **Action**: Use the enhanced `GameStateInspector` with performance profiling to identify heuristics consuming excessive time (>20ms each).
- **Command**:
  ```powershell
  # Profile heuristic performance in a game state
  .\inspect-game-state.ps1 -GameStateFile "<path_to_gamestate.json>" -BotNickname "StaticHeuro" -ProfilePerformance
  ```
- **Analysis**: Look for heuristics exceeding their performance budget. The 200ms total limit should be distributed as:
  - Core movement heuristics: ~100ms
  - Safety heuristics: ~50ms  
  - Path planning heuristics: ~30ms
  - Bonus/penalty heuristics: ~20ms

#### Step 2.1.2: Weight Conflict Detection

- **Action**: Run the `HeuristicConflictAnalyzer` tool to detect when heuristics are working against each other.
- **Command**:
  ```powershell
  # Analyze weight conflicts in recent games
  dotnet run --project tools\\HeuristicConflictAnalyzer -- "logs\\<log_directory>" "StaticHeuro" --analyze-conflicts
  ```
- **Output**: Reports conflicting heuristic pairs and suggests weight adjustments.

#### Step 2.1.3: Automated Weight Tuning

- **Action**: Use the `AutoWeightTuner` to suggest optimal weight adjustments based on performance data.
- **Command**:
  ```powershell
  # Generate weight adjustment suggestions
  dotnet run --project tools\\AutoWeightTuner -- "logs\\<log_directory>" "StaticHeuro" --suggest-weights
  ```
- **Integration**: Tool outputs JSON patches for `heuristic-weights.json` that can be applied automatically.

### 2.2 Long-Term Path Decision Error Detection

**Goal**: Automatically identify cases where StaticHeuro made suboptimal long-term path decisions, particularly around pellet clusters.

#### Step 2.2.1: Path Efficiency Analysis

- **Action**: Run the `PathEfficiencyAnalyzer` to detect inefficient movement patterns.
- **Command**:
  ```powershell
  # Analyze path efficiency over game sequences
  dotnet run --project tools\\PathEfficiencyAnalyzer -- "logs\\<log_directory>" "StaticHeuro" --detect-inefficiencies
  ```
- **Detection Criteria**:
  - Circling behavior (returning to same positions repeatedly)
  - Ignoring large pellet clusters in favor of scattered pellets
  - Taking longer paths when shorter ones were available
  - Missing opportunities to collect multiple pellets in sequence

#### Step 2.2.2: Cluster Targeting Analysis

- **Action**: Use the `ClusterTargetingAnalyzer` to evaluate pellet cluster decision-making.
- **Command**:
  ```powershell
  # Analyze cluster targeting decisions
  dotnet run --project tools\\ClusterTargetingAnalyzer -- "logs\\<log_directory>" "StaticHeuro" --analyze-clusters
  ```
- **Metrics**:
  - Cluster completion rate (% of targeted clusters fully collected)
  - Cluster abandonment patterns (switching targets mid-collection)
  - Optimal vs actual cluster selection (value-based ranking)

#### Step 2.2.3: Automated Test Generation for Path Errors

- **Action**: Automatically generate functional tests for detected path decision errors.
- **Command**:
  ```powershell
  # Generate tests for path decision errors
  .\generate-path-error-tests.ps1 -LogDirectory "logs\\<log_directory>" -BotNickname "StaticHeuro" -ErrorType "cluster_abandonment"
  ```
- **Test Types**:
  - `cluster_abandonment`: Tests for premature cluster switching
  - `inefficient_pathing`: Tests for suboptimal route selection
  - `pellet_prioritization`: Tests for incorrect pellet value assessment

## 3. Scenario-Based Test Creation

This section provides guides for creating tests based on specific, interesting in-game scenarios.

### Scenario 1: Bot is Near a Zookeeper

**Goal**: Create a test to verify a bot's defensive behavior when a zookeeper is nearby or has captured it.

#### Step 1.1: Find the Game State

- **Action**: Use `find_close_zookeeper_state.ps1` to find a relevant game state from your logs.
- **Command**:
  ```powershell
  # This outputs the path to the first relevant game state it finds.
  .\tools\find_close_zookeeper_state.ps1 -LogDirectory "logs\<your_log_directory>" -BotNickname "StaticHeuro"
  ```

#### Step 1.2: Analyze the Game State

- **CRITICAL: Never read `.json` game state files directly using file-reading tools.** This is inefficient, error-prone, and goes against the established workflow.
- **ALWAYS use the `GameStateInspector` tool** to analyze game state files. It provides a structured and accurate view of the game world, including bot nicknames, positions, and map layouts.

- **Action**: Use the `GameStateInspector` on the file you found to determine the bot's legal moves.
- **Command**:
  ```powershell
  # IMPORTANT: You must be in the 'tools/GameStateInspector' directory to run this command.
  # From the project root, run this:
  cd tools\GameStateInspector
  dotnet run -- "<path_to_gamestate.json>" "StaticHeuro"
  cd ..\..
  ```
- **Alternative (Recommended)**: Use the wrapper script from the project root:
  ```powershell
  .\inspect-game-state.ps1 -GameStateFile "<path_to_gamestate.json>" -BotNickname "StaticHeuro"
  ```
- **CRITICAL**: Note the **"LEGAL MOVE ANALYSIS"** output. This is the source of truth for the `AcceptableActions` parameter in the next step.

### Scenario 2: Bot is Stuck or Inefficient

### Scenario 3: Capture Avoidance Analysis

**Goal**: Identify where a bot was captured and determine whether the capture could have been avoided.

#### Step 3.1: Run the CaptureAnalysis tool

- **Action**: Execute the `CaptureAnalysis` console app on a folder containing sequential JSON log files (one per tick).
- **Command**:
  ```powershell
  # Defaults to StaticHeuro if no nickname is provided
  dotnet run --project tools\CaptureAnalysis -- "logs\<your_log_directory>" "StaticHeuro"
  ```

#### Step 3.2: Interpret the output

- The tool prints lines such as:
  ```
  Capture detected at tick 5123: AVOIDABLE
  Capture detected at tick 9876: UNAVOIDABLE
  ```
- *AVOIDABLE* indicates at least one safe legal move existed on the tick before capture.

#### Step 3.3: Create a functional test (optional)

- If the capture appears avoidable, copy the corresponding game-state JSON (the tick **before** the capture) to `FunctionalTests/GameStates/`.
- Use `GameStateInspector` to fetch the legal moves for that state.
- Create a test with `create_test.ps1`, setting `-AcceptableActions` to the safe moves reported by `GameStateInspector`.


> [!NOTE]
> The `CaptureAvoidanceHeuristic` was updated on **2025-07-17** to aggregate risk from *all* Zookeepers rather than just the nearest one. If you create a test that exercises capture scenarios, remember that:
> 1. Moves within **2 tiles** of *any* Zookeeper now incur strong penalties.
> 2. Rewards for moving away scale with the original proximity.
> 3. Stepping onto a Zookeeper tile scores **−10 000**.
>
> When introducing new tests you may need to tune `CaptureAvoidancePenaltyFactor` and `CaptureAvoidanceRewardFactor` in `heuristic-weights.json` if the meta changes.
>
> Future sections will cover automated identification of loops or inefficient pellet collection.


### Step 3: Create the Test

Now, create the functional test using the `create_test.ps1` script.

- **Prerequisite**: Copy the game state file to `FunctionalTests/GameStates/`.
  ```powershell
  cp "<path_to_gamestate.json>" "FunctionalTests/GameStates/"
  ```
- **Action**: Run `create_test.ps1` from the project root with the correct parameters.
- **Command Template**:
  ```powershell
  # Move-to-integer mapping: 1=Up, 2=Down, 3=Left, 4=Right
  .\create_test.ps1 \
    -TestName "DescriptiveTestName" \
    -GameStateFile "<gamestate_file_name.json>" \
    -BotNickName "StaticHeuro" `
    -BotsToTest "StaticHeuro" `
    -Description "A clear description of what this test verifies." `
    -AcceptableActions "3,4" # Example for legal moves: Left, Right
  ```

### Step 4: Run the Test & Verify

Execute the test via the API.

- **Action**: Use `Invoke-RestMethod` to call the `/api/Test/run/{testName}` endpoint.
- **Command**:
  ```powershell
  Invoke-RestMethod -Uri "http://localhost:5008/api/Test/run/DescriptiveTestName" -Method POST | ConvertTo-Json -Depth 5
  ```
- **Result**: Analyze the JSON output. `"success": true` means the test passed. If it's `false`, proceed to the debugging loop.

## 5. The Debugging Loop

If a test fails, follow this exact sequence to debug and fix the issue:

1.  **Analyze Failure**: Read the `errorMessage` in the test result to understand the exception or failure condition.
2.  **Fix the Code**: Modify the relevant C# file (e.g., a heuristic in `Bots/StaticHeuro/Heuristics/`).
> [!TIP]
> When fixing heuristics, always use configurable weights from `heuristicContext.Weights` instead of hardcoded numbers. Add new weight properties to `HeuristicWeights.cs` if needed.
3.  **Restart the API**: This is the most important step. **Your fix will not be applied until you restart.**
    ```powershell
    # IMPORTANT: Run this from the project root directory.
    .\start-api.ps1 -Force
    ```
4.  **Re-run the Test**: Execute the `Invoke-RestMethod` command from Step 4 again.
5.  **Repeat**: Continue this loop until the test passes.

## 6. Troubleshooting & Quick Reference

| Problem | Solution |
| :--- | :--- |
| **Code changes don't work** | You forgot to restart the API. Run `.\start-api.ps1 -Force`. |
| **Parameter error on `create_test.ps1`** | You used the wrong parameter name or format. Refer to the template in Step 3. Common mistakes are `-AcceptableMoves` (wrong) vs. `-AcceptableActions` (correct) or `-TestDescription` (wrong) vs. `-Description` (correct). |
| **Test fails on "Illegal Move"** | The `AcceptableActions` in your test are wrong. Re-run the `GameStateInspector` (Step 1.2) to get the correct legal moves. |
| **"Bot does not have a GetAction(GameState, string) method"** | TestController missing fallback for single-parameter GetAction. Add same fallback logic from JsonDrivenTests.cs to TestController.cs (lines 505-515). Critical fix discovered during StaticHeuro debugging. |
| **Connection Refused** | The API is not running. Run `.\start-api.ps1` from the project root to start it. |
| **`command not found` error** | You are in the wrong directory. All `.ps1` scripts must be run from the project root. Use `cd ..` to navigate up. |
| **`-File` parameter does not exist** | The path to the `.ps1` file was not quoted, causing backslashes to be misinterpreted. Wrap the full path in quotes. Example: `powershell -File '.\start-api.ps1'` |

## 7. Key Lessons Learned from StaticHeuro Bot Debugging

### Critical Success Factors
1. **Follow the Complete Workflow** – The StaticHeuro capture-avoidance fix succeeded because we followed every step systematically.
2. **CaptureAnalysis is Essential** – Revealed multiple avoidable captures that manual log review would have missed.
3. **GameStateInspector Before Testing** – Understanding game context prevented creation of incorrect tests.
4. **Debugging Loop Works** – The analyse → fix → restart API → re-run cycle resolved interface issues efficiently.
5. **TestController Interface Fix** – Added fallback mechanism for `GetAction` method signatures – critical for API compatibility.

### What Made This Workflow Effective
- **Root-Cause Analysis**: CaptureAnalysis exposed heuristic weight imbalances, not logic errors.
- **Targeted Testing**: Created a specific test for tick 274 failure point.
- **Systematic Verification**: API-driven test execution confirmed fix effectiveness.
- **Thorough Documentation**: All changes and findings recorded for future reference.

### Time-Saving Insights
- Always **restart the API** after ANY code changes (most common mistake).
- Use **targeted functional tests** for rapid iteration.
- GameStateInspector output maps directly to test-creation parameters.
- TestController fallback mechanism prevents interface compatibility issues.
