---
Description: Implementation plan for fixing bot-engine desync & performance issues in 07-05-43 match
---

# Implementation Plan – 07-05-43 Bot Repeats / Position-Sync Breakdown

> Goal: implement **all** items listed in section 4. *Fix strategy* of `07-05-43-bot-repeats.md` so that the StaticHeuro bot never sends late / default actions, tracks expectations only when safe, and applies a dynamic budget guard to prevent cascading overruns.

## 1. Performance & Dynamic Budget Guard
- [x] Add `const int HARD_DEADLINE_MS = 180;` and `SOFT_BUDGET_MS = 120` to `HeuroBotService`.
- [x] Start a `Stopwatch` at the **very first** line of `GetAction()`.
- [x] Pass the running stopwatch to `HeuristicsManager.ScoreMove()` (new overload) together with `SOFT_BUDGET_MS`.
- [x] Implement budget guard logic inside `HeuristicsManager.ScoreMove()`:
  - [x] After each heuristic calculation check `stopwatch.ElapsedMilliseconds`.
  - [x] If elapsed > budget, **break** out of the loop (skip remaining heuristics).
  - [x] Log `BudgetExceeded` once per move (diagnostics only at log level Debug).
- [x] Ensure early-tick optimisation (ticks ≤ 5) remains active.

## 2. Late-Action Handling & Expectation Tracking
- [x] Measure total elapsed time in `HeuroBotService.GetAction()` **before** returning the chosen action.
- [x] If `elapsedMs >= HARD_DEADLINE_MS`:
  - [x] Log `CRITICAL_TIMEOUT`.
  - [x] Skip setting `_expectedNextPosition`, `_lastActionSent`, `_lastActionTick`.
  - [x] Return a safe fallback action via `GetSafeFallbackAction()` (or null if engine handles).
- [x] Otherwise (on-time) record expectation data as usual.

## 3. Remove Default “Up” Fallback
- [x] Replace every `return BotAction.Up;` in `HeuroBotService` with `GetSafeFallbackAction(...)` (kept only where no animal context available).
- [x] Ensure `GetSafeFallbackAction()` only returns legal moves and never `null` (fall back to the last accepted action if needed).

## 4. Additional Robustness
- [x] Introduce `_lateTickCount` metric; if 3 consecutive late ticks occur:
  - [x] Switch to **essential heuristics only** until the bot is back under 150 ms.
- [x] Clear `_expectedNextPosition` whenever a late tick occurs to prevent endless mismatch logs.

## 5. Code Touch Points
| File | Change |
| --- | --- |
| `Bots/StaticHeuro/Services/HeuroBotService.cs` | stopwatch, hard/soft limits, late-action guard, fallback cleanup |
| `Bots/StaticHeuro/Heuristics/HeuristicsManager.cs` | new overload accepting stopwatch & budget, break-out logic |
| `Bots/StaticHeuro/Heuristics/IHeuristic.cs` | *no change* |
| `Bots/StaticHeuro/Heuristics/*` | none |

## 6. Testing Strategy
- [ ] Performance Test: synthetic board with 1 500 pellets and 5 zookeepers.
  - [ ] Run 30 ticks – assert max tick time < 170 ms.
- [ ] Desync Regression: replay `20250718_071543` log via CaptureAnalysis – assert zero `DISCREPANCY!` lines after T8.
- [ ] Oscillation Regression: run functional test `StaticHeuro_AntiOscillation_45` – assert ≤10 alternations within 50 ticks.
- [ ] Unit Tests: verify `HeuristicsManager.ScoreMove()` stops evaluating once budget exceeded.

## 7. Roll-Out Steps
- [x] Implement code changes (≤100 LOC per method, follow SOLID).
- [x] Build & run all functional tests (build successful, tests locked by process).
- [ ] Update `07-05-43-bot-repeats.md` with root-cause & fix summary.
- [ ] Commit & push.
