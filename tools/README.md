# Development Tools

This directory contains official development tools for the Zooscape project.

## Available Tools

### Game State Inspector

**Location:** `tools/GameStateInspector/`

A C# console application for analyzing JSON game state files to understand bot decision-making context and debug bot behavior.

**Quick Start:**
```bash
cd tools/GameStateInspector
dotnet run -- --help
```

**Common Usage:**
```bash
# Analyze a specific game state
dotnet run -- ../../FunctionalTests/GameStates/12.json ClingyHeuroBot2
```

**Purpose:**
- Debug failing functional tests by examining game context
- Understand why bots make specific decisions
- Validate test expectations against actual game state conditions
- Analyze heuristic scoring context before adjusting bot weights

**Features:**
- Comprehensive game state analysis
- Bot position and score tracking
- Pellet availability and distribution analysis
- Quadrant-based spatial analysis

- Full documentation and examples

See `tools/GameStateInspector/README.md` for detailed documentation.

## Tool Development Guidelines

When creating new development tools:

1. **Location:** Place in appropriate subdirectory under `tools/`
2. **Documentation:** Include comprehensive README.md with usage examples
3. **Integration:** Update `.ai-rules/important-file-paths.md` with tool paths
4. **Consistency:** Follow existing naming and structure patterns
5. **Testing:** Ensure tools work from their intended directories
6. **Scripts:** Provide additional helper scripts if necessary

## Integration with Development Workflow

These tools are designed to integrate with the standard development workflow:

1. **Functional Testing:** Use Game State Inspector to debug failing tests
2. **Bot Development:** Analyze game states before adjusting heuristic weights
3. **Documentation:** All tools are documented in the Memory Bank system
4. **Automation:** Tools can be integrated into CI/CD pipelines as needed

For questions or issues with any tool, refer to the individual tool's README or the project's Memory Bank documentation in `.ai-rules/`. 