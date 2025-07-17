# Capture Avoidance Heuristic Update (2025-07-17)

## Status
Accepted

## Context
The `StaticHeuro` bot was still experiencing avoidable captures, particularly when multiple Zookeepers were in close proximity. The previous `CaptureAvoidanceHeuristic` only evaluated the *nearest* Zookeeper, allowing the bot to hover dangerously near others.

## Decision
Revise `CaptureAvoidanceHeuristic` to consider **all** Zookeepers when scoring a move:

1. **Fatal Move Check** – Moving onto a Zookeeper tile returns a −10 000 score.
2. **Immediate Danger Zone (≤ 2 tiles)** – Apply strong, per-keeper penalties scaled by proximity.
3. **Moving Closer** – Penalise moves that reduce the distance to any Zookeeper.  The penalty scales with how much closer the bot gets relative to the new distance.
4. **Moving Away** – Reward moves that increase distance from a Zookeeper, inversely proportional to the original distance (escaping close threats is worth more).
5. **Noise Reduction** – Clamp very small aggregated scores (−0.01 < score < 0.01) to exactly 0.

This aggregation yields a single score representing combined capture risk across the board.

## Consequences
- The bot now strongly avoids lingering near *any* Zookeeper, not just the nearest.
- Previously failing functional test **GameState 12 – StaticHeuro Must Move Down** now passes.
- CaptureAnalysis runs show a marked reduction in avoidable captures for StaticHeuro.
- Future tuning should adjust `CaptureAvoidancePenaltyFactor` and `CaptureAvoidanceRewardFactor` if the meta changes.

---
**Changed Files**
- `Bots/StaticHeuro/Heuristics/CaptureAvoidanceHeuristic.cs`

**Related Tickets / Commits**
- Heuristic update committed on 2025-07-17.
