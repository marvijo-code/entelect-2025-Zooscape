# Bot Analysis Documentation

This folder contains logically organized documentation files extracted from the original `.github/chatmodes/AutomatedLogAnalysisTestCreation.chatmode.md` workflow.  Each file focuses on a specific aspect of bot analysis, debugging, or test-creation so you can jump directly to the topic you need without scrolling through a single monolithic document. Always improve the StaticHeuro bot!!!

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
| **start-api.ps1** | project root | Rebuilds and restarts the FunctionalTests API (Golden Rule). | `.\start-api.ps1 -Force` |

### Running a Single Functional Test

You can execute an individual test case in **two** ways:

1. **Via the API** (preferred for quick loops):
   ```powershell
   Invoke-RestMethod -Method POST -Uri "http://localhost:5000/api/test/run/StaticHeuro_AdjacentPellet_953"
   ```

2. **Via dotnet test filter:**
   ```powershell
   dotnet test --filter "FullyQualifiedName~StaticHeuro_AdjacentPellet_953"
   ```

Remember to **restart the API** (`start-api.ps1 -Force`) after any code or weight change before rerunning the test.
