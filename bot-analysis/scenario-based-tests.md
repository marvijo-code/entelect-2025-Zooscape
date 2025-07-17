---
description: "Scenario-Based Functional Test Creation"
---

# Scenario-Based Functional Test Creation Guide

This document walks you through creating targeted functional tests for specific in-game situations.  Always follow the **Critical Workflow** first (see `critical-workflow.md`) and remember to **restart the API** after adding or modifying tests.

---

## 1. Bot Near a Zookeeper (Defensive Behaviour)

### 1.1 Locate a Game-State
Run the helper script to scan logs for states where a zookeeper is adjacent to, or has just captured, the bot:

```powershell
.\tools\find_close_zookeeper_state.ps1 -LogDirectory "logs\<dir>" -BotNickname "StaticHeuro"
```

### 1.2 Analyse with GameStateInspector *(never open the JSON directly!)*

```powershell
cd tools\GameStateInspector
 dotnet run -- "<path_to_state.json>" "StaticHeuro"
cd ..\..
# or simply
.\inspect-game-state.ps1 -GameStateFile "<state.json>" -BotNickname "StaticHeuro"
```

Take note of the **LEGAL MOVE ANALYSIS**; those moves become your `AcceptableActions`.

### 1.3 Create the Test

```powershell
.\create_test.ps1 -GameStateFile "<state.json>" -BotNickname "StaticHeuro" -AcceptableActions "Up,Left" -TestName "StaticHeuro_Defensive_ZK_<tick>" -Description "Bot chooses safe move away from zookeeper"
```

---

## 2. Bot Is Stuck / Inefficient

*Coming soon – use PathEfficiencyAnalyzer to locate inefficient ticks, then repeat the process above.*

---

## 3. Capture-Avoidance Verification

### 3.1 Run CaptureAnalysis

```powershell
dotnet run --project tools\CaptureAnalysis -- "logs\<dir>" "StaticHeuro"
```

Lines marked **AVOIDABLE** indicate a potential test candidate (take the tick **before** capture).

### 3.2 Build the Test
1. Copy the game-state JSON for the tick before capture to `FunctionalTests/GameStates/`.
2. Run GameStateInspector to list legal moves.
3. Use `create_test.ps1` with the legal safe moves.

> **Note** – CaptureAvoidanceHeuristic was updated on *2025-07-17* to aggregate risk from **all** zookeepers.  Adjust `CaptureAvoidancePenaltyFactor` & `CaptureAvoidanceRewardFactor` if the meta shifts.
