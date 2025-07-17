---
description: "Case Study – StaticHeuro Pellet Collection Debug (2025-07-17)"
---

# Lessons Learned – StaticHeuro Pellet Collection Debug

## Key File Paths for Bot Debugging

| Purpose | Path |
|---------|------|
| Main Bot Logic | `Bots/StaticHeuroBot/Program.cs` |
| Heuristic Weights | `Bots/StaticHeuroBot/heuristic-weights.json` |
| Bot Service | `Bots/StaticHeuroBot/Services/HeuroBotService.cs` |
| Consolidated Functional Tests | `FunctionalTests/TestDefinitions/ConsolidatedTests.json` |
| Game-State Samples | `FunctionalTests/GameStates/*.json` |
| Test Framework | `FunctionalTests/JsonDrivenTests.cs` |
| Test Definition Loader | `FunctionalTests/Services/TestDefinitionLoader.cs` |
| Common Heuristics Directory | `Marvijo.Zooscape.Bots.Common/Heuristics/` |
| Key Heuristic Files | `PelletEfficiencyHeuristic.cs`, `WallCollisionRiskHeuristic.cs`, `CaptureAvoidanceHeuristic.cs` |

---

## Debugging Workflow for Adjacent-Pellet Issues

```powershell
# 1️⃣ Run functional tests to surface failures
cd FunctionalTests
 dotnet test --filter "FullyQualifiedName~StaticHeuro"

# 2️⃣ Analyse specific game-state with GameStateInspector
cd ..\tools\GameStateInspector
 dotnet run -- "..\..\FunctionalTests\GameStates\953.json" "StaticHeuro"

# 3️⃣ Check heuristic balance
#   • WallCollisionRisk penalties vs PelletEfficiency rewards
#   • ImmediatePelletBonus should be ≥ 500

# 4️⃣ Create a targeted functional test
#   • Add definition to ConsolidatedTests.json
#   • Verify expected action matches bot decision
```

### Common Fixes

* **Scope Issues** – Move variable declarations to broader scope in `Program.cs`.
* **Bot-ID Issues** – Remove duplicate `SetBotId` methods in bot services.
* **Heuristic Balance** – Increase pellet rewards, decrease wall-collision penalties.
* **Compilation Errors** – Fix ambiguous references (e.g., `System.IO.File` vs `File`).

---

## Performance Tips

```powershell
# Run only StaticHeuro tests
 dotnet test --filter "FullyQualifiedName~StaticHeuro" --logger "console;verbosity=minimal"

# Run a specific test by name (if framework supports it)
 dotnet test --filter "TestName~StaticHeuro_AdjacentPellet_953"
```

* **Quick weight tweaks** – Edit `heuristic-weights.json`, then **restart the API** `.\start-api.ps1 -Force` and rerun the targeted test.
* **Watch for duplicate methods / missing `using`s / variable scope bugs**.
