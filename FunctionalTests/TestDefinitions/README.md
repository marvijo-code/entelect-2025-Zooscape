# JSON-Driven Functional Tests

This directory contains JSON test definitions that allow you to create new functional tests without modifying C# code.

## How It Works

The `JsonDrivenTests` class automatically loads all `.json` files from this directory and executes them as xUnit tests. This provides two main advantages:

1. **No Code Changes Required**: Add new tests by simply creating JSON files
2. **Data-Driven Testing**: Modify test parameters without recompilation

## Test Definition Schema

Each JSON file can contain either a single test definition or an array of test definitions.

### Required Properties

- `testName`: Unique name for the test case
- `gameStateFile`: Name of the game state JSON file (e.g., "34.json", "162.json")
- `testType`: Type of test to perform (see Test Types below)

### Optional Properties

- `description`: Human-readable description of what the test validates
- `botNickname`: Specific bot nickname to test against
- `expectedAction`: Expected action from the bot ("Up", "Down", "Left", "Right")
- `acceptableActions`: Array of acceptable actions
- `tickOverride`: Boolean for tick override functionality testing
- `bots`: Array of bot types for multi-bot tests

## Test Types

### 1. GameStateLoad
Validates that a game state JSON file loads correctly without errors.

```json
{
  "testName": "LoadTest_GameState34",
  "gameStateFile": "34.json",
  "testType": "GameStateLoad",
  "description": "Validate game state 34 loads correctly"
}
```

### 2. SingleBot
Tests a single bot's behavior against a game state.

```json
{
  "testName": "SingleBot_ExpectedUp",
  "gameStateFile": "162.json",
  "testType": "SingleBot",
  "expectedAction": "Up",
  "description": "Test that bot returns expected Up action"
}
```

### 3. MultiBotArray
Tests multiple bots against the same game state for comparison.

```json
{
  "testName": "BotComparison_LeftOrDown",
  "gameStateFile": "34.json",
  "testType": "MultiBotArray",
  "acceptableActions": ["Down", "Left"],
  "bots": ["ClingyHeuroBot2", "ClingyHeuroBot"],
  "description": "Compare both bots' pellet-chasing behavior"
}
```

### 4. TickOverride
Tests tick override functionality.

```json
{
  "testName": "TickOverrideTest",
  "gameStateFile": "34.json",
  "testType": "TickOverride",
  "tickOverride": true,
  "description": "Test tick override functionality"
}
```

## Available Bot Types

- `ClingyHeuroBot2`: The primary heuristic bot implementation
- `ClingyHeuroBot`: Alternative heuristic bot variant

## Example Files

- `GameState34Tests.json`: Basic tests for game state 34
- `AdvancedBotTests.json`: More complex scenarios and comparisons

## Adding New Tests

1. Create a new `.json` file in this directory
2. Define your test cases following the schema above
3. Run the tests - new definitions will be automatically discovered

No code changes or recompilation required!

## Execution

Tests are executed by the `JsonDrivenTests` class:

- `ExecuteAllJsonDefinedTests()`: Runs all JSON-defined tests in a single test method
- `ExecuteIndividualJsonTest()`: Runs each JSON test as a separate xUnit test (via Theory/MemberData)

Both approaches provide detailed logging and clear error reporting. 