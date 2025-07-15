---
description: 'Automated Log Analysis & Test Creation: Analyze game logs, create, and run functional tests via API to iteratively improve bot heuristics.'
name: AutomatedLogAnalysisTestCreation
---

# Automated Log Analysis & Test Creation Workflow

## 1. Purpose
This workflow provides a clear, step-by-step guide to analyze game state logs, create new functional tests via the API, and validate bot behavior. Following this guide will prevent common issues and streamline the process of improving bot heuristics.

## 2. Quick Reference: Key Files & Directories

| Item | Path | Purpose |
| :--- | :--- | :--- |
| **Game State Logs** | `logs/` | Contains raw game state JSON files for analysis. |
| **Game State Inspector**| `tools/GameStateInspector/` | C# tool to analyze a game state file and determine legal moves. |
| **Create Test Script** | `create_test.ps1` | PowerShell script to create a new test via the API. |
| **Test API Project** | `FunctionalTests/` | The ASP.NET project containing the test API and test logic. |
| **Test Definitions** | `FunctionalTests/TestDefinitions/ConsolidatedTests.json` | The single source of truth for all JSON-driven tests. |
| **Game States for Tests**| `FunctionalTests/GameStates/` | Directory where game state files used in tests must be stored. |
| **Heuristic Weights** | `Bots/StaticHeuro/heuristic-weights.json` | Configuration file for the bot's decision weights. |

## 3. The Workflow: Step-by-Step

### Step 1: Start the Test API
The functional test project runs a web API that is essential for creating and running tests.

- **Action**: Run the `rv-run-visualizer.ps1` script. This starts the API.
- **Command**:
  ```powershell
  # This script starts the API on http://localhost:5008
  ./rv-run-visualizer.ps1
  ```
- **Note**: Keep this terminal window open. The API must be running for the entire workflow.

### Step 2: Analyze a Game State
Select a log file and use the `GameStateInspector` to understand the strategic context and, most importantly, determine the **legal moves**.

- **Action**: Run the inspector tool, pointing it to a specific game state JSON file and the bot you want to analyze.
- **Command**:
  ```powershell
  # Replace the path and bot name as needed
  dotnet run --project tools/GameStateInspector -- "logs/20250715-064223/100_100_3.json" "ClingyHeuroBot"
  ```
- **CRITICAL**: Note the **"LEGAL MOVE ANALYSIS"** section in the output. This is the source of truth for a bot's available actions.

### Step 3: Create a New Test
Use the `create_test.ps1` script to send a request to the running API. This will create the test definition and save the associated game state.

- **Prerequisite**: First, copy the game state JSON file from its `logs` directory to the `FunctionalTests/GameStates/` directory. The script needs the file to be in this location.
  ```powershell
  # Example:
  cp logs/20250715_064223/500.json FunctionalTests/GameStates/
  ```

- **Action**: Run the `create_test.ps1` script with the required parameters.
- **Command**:
  ```powershell
  # Note the format for AcceptableActions is a comma-separated string of integers.
  ./create_test.ps1 \
    -TestName "ClingyHeuroBot_Scenario_1" \
    -GameStateFile "500.json" \
    -BotNicknameInState "ClingyHeuroBot" \
    -AcceptableActions "3,4" # 1:Up, 2:Down, 3:Left, 4:Right
  ```
- **Verification**: The script should report success. You can also check `FunctionalTests/TestDefinitions/ConsolidatedTests.json` to see your new test definition.

### Step 4: Run Your Specific Test
Run the newly created test by calling the dedicated API endpoint. **Do not use `dotnet test` with a filter.**

- **Action**: Use `Invoke-RestMethod` to call the `/api/Test/run/{testName}` endpoint.
- **Command**:
  ```powershell
  # Replace the test name with the one you just created
  Invoke-RestMethod -Uri "http://localhost:5008/api/Test/run/ClingyHeuroBot_Scenario_1" -Method Post
  ```
- **Result**: The output will show if the test passed (`success: True`) and which action the bot took.

### Step 5: Iteratively Improve Heuristics (If Needed)
If the test fails, or if the bot's choice was suboptimal, adjust the weights in `Bots/StaticHeuro/heuristic-weights.json` and re-run the test until it passes.

## Troubleshooting Common Issues

| Problem | Solution |
| :--- | :--- |
| **`create_test.ps1` fails with a parameter error** | The `-AcceptableActions` parameter expects a **comma-separated string of integers**, not text. The mapping is: `1`=Up, `2`=Down, `3`=Left, `4`=Right. Example: `-AcceptableActions "3,4"`. |
| **Test fails with "File Not Found" at runtime** | This happens if the test runner looks for game state files in the wrong directory (e.g., `bin/Release`). **Solution**: The file `FunctionalTests/BotTestHelper.cs` has been fixed to build a robust path from the project root. If this error reappears, ensure the API was **rebuilt and restarted** after any code changes. |
| **Connection Refused / API Not Responding** | Ensure the API is running. Check the terminal where you ran `rv-run-visualizer.ps1` in Step 1. The API must be active on `http://localhost:5008`. |
| **Test Fails on an "Illegal Move"** | The `AcceptableActions` for your test are incorrect. Re-run the `GameStateInspector` (Step 2) to get the correct list of legal moves and update your test definition. |
| **Test Not Found or Not Running** | You are likely using `dotnet test --filter`. **Do not do this.** Run specific tests via the API endpoint as shown in Step 4. The `dotnet test` command is only for running the *entire* test suite. |
