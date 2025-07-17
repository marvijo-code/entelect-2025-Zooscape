---
description: "Troubleshooting & Quick Reference"
---

# Troubleshooting & Quick Reference

This cheat-sheet consolidates the most common issues encountered when analysing bots and running functional tests, along with the quick fixes that have proven to work.

| Problem | Solution |
|---------|----------|
| **Code changes don’t work** | You forgot to **restart the API**: `.\start-api.ps1 -Force` |
| **Parameter error on `create_test.ps1`** | Check parameter names: `-AcceptableActions`, `-Description`, etc. |
| **Test fails on "Illegal Move"** | Use **GameStateInspector** to update `AcceptableActions` with the correct legal moves. |
| **"Bot does not have a GetAction(GameState, string) method"** | Add the reflection **fallback** to `TestController.cs` – copy logic from `JsonDrivenTests.cs` (lines ~505-515). |
| **Connection Refused** | API isn’t running – run `.\start-api.ps1` from the project root. |
| **`command not found` error** | You’re in the wrong dir – all `.ps1` scripts run from project root. |
| **`-File` parameter does not exist** | Path not quoted – wrap `.ps1` path in single quotes. |

---

## Key Lessons Recap (StaticHeuro Debug Session)

1. Always **follow the complete workflow** – skipping steps wastes time.
2. **CaptureAnalysis + GameStateInspector** are essential first-line tools.
3. The **analyse → fix → restart API → re-run** loop is the fastest way to converge on a fix.
4. **Interface compatibility** – ensure TestController has the two-parameter → one-parameter fallback for `GetAction`.
5. **Restart the API** after any code or weight change.
6. Use **targeted functional tests** for rapid iteration.
