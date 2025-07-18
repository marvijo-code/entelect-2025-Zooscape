# Generalized Test Creation System

## CRITICAL REQUIREMENT

**BEFORE STARTING ANY DEBUGGING, TESTING, OR LOG ANALYSIS WORK:**

**YOU MUST FIRST READ THE ENTIRE FILE:** `.github/chatmodes/AutomatedLogAnalysisTestCreation.chatmode.md`

This file contains the complete workflow for:
- Log analysis using CaptureAnalysis tool
- Game state inspection using GameStateInspector
- Proper test creation methodology
- Debugging loop for resolving test failures
- API usage patterns and troubleshooting

**Failure to follow this workflow will result in inefficient debugging and missed critical steps.**

---

This system provides a flexible way to create automated tests for bot behavior analysis using PowerShell scripts.

## Files

- **`create_test.ps1`** - Main generalized script for creating tests
- **`create_test_examples.ps1`** - Examples and usage demonstrations
- **`README-TestCreation.md`** - This documentation file

## Quick Start

### Basic Usage
```bash
# Use pwsh for cross-platform PowerShell
pwsh
# Then in pwsh:

# Create a simple test
.\create_test.ps1 -GameStateFile "tick_1100.json" -TestName "MyTest"

# Create a test with specific bot nickname and bots to test
.\create_test.ps1 `
    -GameStateFile "tick_1100.json" `
    -TestName "StaticHeuro_Analysis" `
    -BotNicknameInState "StaticHeuro" `
    -BotsToTest @("StaticHeuro") `
    -Description "Test StaticHeuro decision making"
```

### Enhanced API Debug Output

The test API endpoint now returns detailed bot debug scores in a flattened, terminal-friendly format:

```bash
# Use pwsh for cross-platform PowerShell
pwsh
# Then in pwsh:

# Run a test and get detailed debug information
$result = Invoke-RestMethod -Uri 'http://localhost:5008/api/test/run/StaticHeuro_AdjacentPellet_953' -Method POST

# Access bot decision and scores directly
Write-Host "Bot chose: $($result.botResults[0].action)"
Write-Host "Success: $($result.success)"

# Get individual action scores (flattened structure)
Write-Host "Action Scores:"
Write-Host "  Right: $($result.botResults[0].performanceMetrics.ActionScore_Right)"
Write-Host "  Left: $($result.botResults[0].performanceMetrics.ActionScore_Left)"
Write-Host "  Up: $($result.botResults[0].performanceMetrics.ActionScore_Up)"
Write-Host "  Down: $($result.botResults[0].performanceMetrics.ActionScore_Down)"

# Get detailed heuristic breakdown for specific moves
$rightDetails = $result.botResults[0].performanceMetrics.DetailedScore_Right_LogLines
Write-Host "Right move heuristic details:"
Write-Host $rightDetails
```

**Key Improvements:**
- **No deep JSON serialization needed** - simple `ConvertTo-Json` works without `-Depth 10`
- **Direct access to action scores** via `ActionScore_[Action]` properties
- **Clean terminal output** - log lines normalized without `\r\n` characters
- **Detailed heuristic breakdowns** available via `DetailedScore_[Action]_LogLines`

### Advanced Usage
```powershell
# Create a test with limited acceptable actions
.\create_test.ps1 `
    -GameStateFile "tick_1100.json" `
    -TestName "Limited_Actions_Test" `
    -BotNicknameInState "ClingyHeuroBot2" `
    -BotsToTest @("ClingyHeuroBot2") `
    -AcceptableActions @(1, 3) `
    -Description "Bot should choose Up or Left only"

# Create a multi-bot comparison test
.\create_test.ps1 `
    -GameStateFile "complex.json" `
    -TestName "Multi_Bot_Comparison" `
    -BotsToTest @("ClingyHeuroBot2", "StaticHeuro", "ClingyHeuroBot") `
    -TestType "MultiBotArray" `
    -Description "Compare multiple bots on same game state"
```

## Parameters

### Required Parameters
- **`-GameStateFile`** - Name of the JSON file in `FunctionalTests/GameStates/`
- **`-TestName`** - Unique name for the test

### Optional Parameters
- **`-BotNicknameInState`** - Nickname of bot in the game state (default: `null` - uses first animal)
- **`-BotsToTest`** - Array of bot names to test (default: `@("ClingyHeuroBot2")`)
- **`-Description`** - Test description (default: `"Automated test"`)
- **`-AcceptableActions`** - Array of action IDs (default: `@(1,2,3,4)`)
- **`-TestType`** - Type of test (default: `"SingleBot"`)
- **`-TickOverride`** - Enable tick override (default: `$false`)
- **`-ApiUrl`** - API endpoint (default: `"http://localhost:5008/api/test/create"`)
- **`-GameStatesDir`** - Directory containing game states (default: `"FunctionalTests/GameStates"`)

## Action IDs

The `AcceptableActions` parameter uses integer IDs:
- **1** - Up
- **2** - Down  
- **3** - Left
- **4** - Right

## Bot Nickname vs Bots to Test

- **`BotNicknameInState`**: Specifies which animal in the game state to test (by nickname). If `null`, uses the first animal.
- **`BotsToTest`**: Specifies which bot implementations to use for testing. Can be multiple bots for comparison tests.

## Common Use Cases

### 1. Automated Log Analysis Workflow
```powershell
# Generate timestamp-based test name
$timestamp = Get-Date -Format "yyyyMMdd_HHmm"
$tickNumber = "1100"
$testName = "AutoGen_${timestamp}_${tickNumber}_Analysis"

.\create_test.ps1 `
    -GameStateFile "tick_1100.json" `
    -TestName $testName `
    -BotNicknameInState "ClingyHeuroBot2" `
    -BotsToTest @("ClingyHeuroBot2") `
    -AcceptableActions @(1, 3, 2) `
    -Description "Automated test for tick $tickNumber - strategic analysis"
```

### 2. Bot Comparison Testing
```powershell
# Test multiple bots on same game state
$bots = @("ClingyHeuroBot2", "ClingyHeuroBot", "StaticHeuro")
$gameState = "162.json"
$timestamp = Get-Date -Format "yyyyMMdd_HHmm"

# Single test with multiple bots
.\create_test.ps1 `
    -GameStateFile $gameState `
    -TestName "Comparison_${timestamp}_MultiBots" `
    -BotsToTest $bots `
    -TestType "MultiBotArray" `
    -Description "Compare multiple bots on complex game state"

# OR create separate tests for each bot
foreach ($bot in $bots) {
    $testName = "Comparison_${timestamp}_${bot}"
    .\create_test.ps1 `
        -GameStateFile $gameState `
        -TestName $testName `
        -BotNicknameInState $bot `
        -BotsToTest @($bot) `
        -Description "Individual test for $bot performance"
}
```

### 3. Strategic Decision Testing
```powershell
# Test specific strategic scenarios
.\create_test.ps1 `
    -GameStateFile "complex_scenario.json" `
    -TestName "Strategic_Decision_Test" `
    -BotNicknameInState "ClingyHeuroBot2" `
    -BotsToTest @("ClingyHeuroBot2") `
    -AcceptableActions @(3) `
    -Description "Bot should choose Left due to superior pellet clustering"
```

### 4. Testing Without Bot Nickname
```powershell
# Test using first animal in game state (no specific nickname)
.\create_test.ps1 `
    -GameStateFile "tick_1100.json" `
    -TestName "First_Animal_Test" `
    -BotsToTest @("ClingyHeuroBot2") `
    -Description "Test using first animal in game state"
```

## Integration with GameStateInspector

Use the `GameStateInspector` tool to analyze game states before creating tests:

```bash
# Analyze game state first
dotnet run --project tools/GameStateInspector -- "logs/path/to/state.json" "ClingyHeuroBot2"

# Then create test based on analysis
.\create_test.ps1 `
    -GameStateFile "analyzed_state.json" `
    -TestName "Inspector_Based_Test" `
    -BotNicknameInState "ClingyHeuroBot2" `
    -BotsToTest @("ClingyHeuroBot2") `
    -AcceptableActions @(1, 3) `
    -Description "Test based on GameStateInspector analysis"
```

## Error Handling

The script includes comprehensive error handling:
- Validates game state file existence
- Provides clear error messages
- Shows request body on API failures
- Colored output for better visibility

## Running Tests

After creating tests, run them with:
```bash
# Run all JSON-driven tests
dotnet test FunctionalTests/FunctionalTests.csproj --filter "FullyQualifiedName~JsonDrivenTests"

# Run with verbose output
dotnet test FunctionalTests/FunctionalTests.csproj --filter "FullyQualifiedName~JsonDrivenTests" --logger "console;verbosity=normal"
```

## Best Practices

1. **Use descriptive test names** - Include timestamp, tick number, and purpose
2. **Set appropriate acceptable actions** - Use GameStateInspector to determine legal moves
3. **Include meaningful descriptions** - Document the strategic context
4. **Test incrementally** - Start with broad acceptable actions, then narrow down
5. **Use bot nicknames when available** - More precise than using first animal
6. **Batch similar tests** - Use loops for comparing multiple bots

## Troubleshooting

### Common Issues

1. **Game state file not found**
   - Ensure the file exists in `FunctionalTests/GameStates/`
   - Check the exact filename and extension

2. **API connection failed**
   - Verify the API is running: `powershell -ExecutionPolicy Bypass -File rv-run-visualizer.ps1`
   - Check the API URL parameter

3. **Invalid acceptable actions**
   - Use only valid action IDs: 1 (Up), 2 (Down), 3 (Left), 4 (Right)
   - Ensure actions are legal moves for the game state

4. **Bot nickname not found**
   - Check that the bot nickname exists in the game state
   - Use `null` to default to first animal

5. **Test name conflicts**
   - Use unique test names
   - Include timestamps to avoid duplicates

## API Structure

The script sends requests with the following structure:
```json
{
    "TestName": "string",
    "GameStateFile": "string",
    "CurrentGameState": {...},
    "TestType": "SingleBot|MultiBotArray|...",
    "BotNickname": "string|null",
    "BotsToTest": ["string", "string", ...],
    "Description": "string",
    "TickOverride": boolean,
    "AcceptableActions": [1, 2, 3, 4]
}
```

## Examples

Run `.\create_test_examples.ps1` to see all usage examples in action. 