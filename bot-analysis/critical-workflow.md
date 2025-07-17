---
description: "Critical Workflow Instructions – Read First"
---

# 🚨 CRITICAL WORKFLOW – READ THIS FIRST 🚨

> **MANDATORY** – Before starting ANY debugging, testing, or log-analysis work you **MUST** read and follow this workflow in full. It has been battle-tested through real debugging scenarios (e.g., StaticHeuro capture-avoidance fix). Skipping steps leads to wasted time, missed root causes, and interface-compatibility issues.

---

## 1. The Golden Rule – Always Restart the API

The `FunctionalTests` API server compiles and loads bot logic on startup. **Any time you change code** (heuristics, services, project files, etc.) run:

```powershell
# Run from project root
.\start-api.ps1 -Force
```

Without a restart you are testing stale code.

---

## 2. Core Tools

| Tool | Purpose |
|------|---------|
| **GameStateInspector** | Inspect a JSON game-state file and list legal moves. |
| **CaptureAnalysis** | Scan a match log folder for avoidable captures. |
| **create_test.ps1** | Generate functional-test JSON stubs from a game state. |
| **TestController API** | Run a single functional test via `POST /api/test/run/{testName}`. |

---

## 3. Debugging Loop

1. **Identify failure** (functional test, CaptureAnalysis, or log observation).
2. **Analyse game state** with GameStateInspector.
3. **Fix code / adjust weights**.
4. **Restart API** – see Golden Rule.
5. **Re-run the test**.
6. **Repeat** until the test passes.

---

## 4. Common Pitfalls & Quick Fixes

| Problem | Quick Fix |
|---------|-----------|
| *Code changes don’t apply* | You forgot to restart the API. |
| *API error* `Bot does not have GetAction(GameState, string)` | Add the **fallback GetAction** reflection logic to `TestController.cs` (see troubleshooting.md). |
| *Illegal Move* failures | Use GameStateInspector to update `AcceptableActions`. |
| *create_test.ps1 parameter* errors | Check parameter names: `-AcceptableActions`, `-Description`, etc. |
| *`command not found`* / *`Connection Refused`* | Wrong working directory or API is not running. |

---

**Remember:** All other documentation in this folder builds on these instructions. Read this file thoroughly before anything else.
