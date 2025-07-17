---
description: "Full List of Available CLI Tools & Helper Scripts"
---

# Available Tools & Helper Scripts

This document provides an explicit, central reference for every **official** CLI tool or PowerShell helper script that is part of the Zooscape repo.  Use this as a quick lookup for purpose, location, and example usage.

> **Convention:** All commands are expected to be run from the **project root** unless the example explicitly `cd`s into a sub-folder.

| Name | Type | Location | Purpose | Example Command |
|------|------|----------|---------|-----------------|
| **GameStateInspector** | .NET console app | `tools/GameStateInspector/` | Analyse a JSON game-state file, list legal moves, profile heuristic timing. | `cd tools/GameStateInspector && dotnet run -- ../../FunctionalTests/GameStates/12.json StaticHeuro` |
| **CaptureAnalysis** | .NET console app | `tools/CaptureAnalysis/` | Scan a log-folder for captures and mark each as *AVOIDABLE* or *UNAVOIDABLE*. | `dotnet run --project tools/CaptureAnalysis -- "logs/20250717_match" StaticHeuro` |
| **FindPelletIgnoreStates** | .NET console app | `tools/FindPelletIgnoreStates/` | Locate ticks where the bot ignored adjacent pellets. | `dotnet run --project tools/FindPelletIgnoreStates -- "logs/20250717_match" StaticHeuro` |
| **inspect-game-state.ps1** | PowerShell | project root | Wrapper that invokes GameStateInspector from any dir. | `.\inspect-game-state.ps1 -GameStateFile "FunctionalTests/GameStates/953.json" -BotNickname "StaticHeuro"` |
| **find_close_zookeeper_state.ps1** | PowerShell | project root | Search logs for a state where a zookeeper is one tile away from the bot. | `.\find_close_zookeeper_state.ps1 -LogDirectory "logs/20250717_match" -BotNickname "StaticHeuro"` |
| **create_test.ps1** | PowerShell | project root | Generate a functional-test JSON stub from a game-state. | `.\create_test.ps1 -GameStateFile "FunctionalTests/GameStates/953.json" -BotNickname "StaticHeuro" -AcceptableActions "Right"` |
| **generate-path-error-tests.ps1** | PowerShell | project root | Auto-generate tests for path-decision errors (`cluster_abandonment`, etc.). | `.\generate-path-error-tests.ps1 -LogDirectory "logs/20250717_match" -BotNickname "StaticHeuro" -ErrorType "cluster_abandonment"` |
| **start-api.ps1** | PowerShell | project root | Rebuild and restart FunctionalTests API (**Golden Rule**). | `.\start-api.ps1 -Force` |
| **run-all-tests.ps1** | PowerShell | project root | Convenience script to run **every** functional test. | `.\run-all-tests.ps1` |

## Running a Single Functional Test

1. **Via the REST API** (fastest feedback loop):
   ```powershell
   Invoke-RestMethod -Method POST -Uri "http://localhost:5000/api/test/run/StaticHeuro_AdjacentPellet_953"
   ```
2. **Via dotnet test filter:**
   ```powershell
   dotnet test --filter "FullyQualifiedName~StaticHeuro_AdjacentPellet_953"
   ```

## Adding New Tools

1. Place code in a sub-folder under `tools/` (for .NET) or at project root (for simpler .ps1 scripts).
2. Include a README with purpose, usage, and examples.
3. Update this `available-tools.md` table and `.ai-rules/important-file-paths.md` accordingly.

---

**Remember:** After changing any bot code, heuristic weights, or adding tests, **restart the API** with `start-api.ps1 -Force` before rerunning tests.
