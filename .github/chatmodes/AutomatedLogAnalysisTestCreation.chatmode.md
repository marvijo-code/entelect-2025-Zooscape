---
description: 'Automated Log Analysis & Test Creation: Analyze game logs, create, and run functional tests via API to iteratively improve bot heuristics.'
name: AutomatedLogAnalysisTestCreation
---

# Automated Log Analysis & Test Creation Workflow

## 1. The Golden Rule: Always Restart the API

> [!IMPORTANT]
> The `FunctionalTests` API server compiles and loads bot logic on startup. If you make **any code changes** to a bot's heuristics, services, or any related files, you **MUST** restart the API. Otherwise, your changes will not take effect, and you will be testing against stale code.

**Use this command to force a rebuild and restart:**
```powershell
# Run from the project root
.\start-api.ps1 -Force
```

## 2. Scenario-Based Test Creation

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
| **Connection Refused** | The API is not running. Run `.\start-api.ps1` from the project root to start it. |
| **`command not found` error** | You are in the wrong directory. All `.ps1` scripts must be run from the project root. Use `cd ..` to navigate up. |
| **`-File` parameter does not exist** | The path to the `.ps1` file was not quoted, causing backslashes to be misinterpreted. Wrap the full path in quotes. Example: `powershell -File '.\start-api.ps1'` |
