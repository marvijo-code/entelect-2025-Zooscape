# ADR-003: AdvancedMCTSBot Overhaul for Performance and Correctness

- **Date**: 2025-07-14
- **Status**: Implemented

## Context and Problem Statement

The `AdvancedMCTSBot` exhibits severe performance issues, including getting stuck in repetitive loops (e.g., moving Up-Down repeatedly) and failing to maximize rewards by collecting pellets. This behavior indicates fundamental flaws in its Monte Carlo Tree Search (MCTS) implementation.

A detailed code review and analysis revealed several root causes:

1.  **Incorrect Action Selection**: The final action selection criterion in `findBestAction` uses the **highest visit count** instead of the **highest value (average reward)**. This is the primary cause of the bot getting stuck, as it favors explored but low-value moves over potentially better but less-explored paths.
2.  **Incomplete Transposition Table**: The implementation includes a transposition table to store and retrieve previously seen game states, but it is not fully integrated. The `store` function is never called, meaning the tree cannot recognize and merge identical states reached via different move sequences. This leads to redundant computation and prevents the bot from learning about cycles.
3.  **No Explicit Cycle Detection**: The simulation (rollout) phase lacks any mechanism to detect or penalize state repetitions. The bot can simulate endless loops without any negative feedback, making these paths seem neutral or even appealing if they are near a previously high-reward state.
4.  **Imbalanced Reward Shaping**: The reward values for immediate pellet collection are disproportionately high compared to any long-term strategic rewards. Once nearby pellets are consumed, the bot has no incentive to explore further, causing it to oscillate around the last high-reward area.
5.  **Flawed `UseItem` Logic**: The simulation policy for `UseItem` actions contains a `continue` statement that prevents the action's score from being evaluated, effectively making the bot ignore its power-ups during rollouts.

## Decision

We will implement a series of targeted fixes to the MCTS core logic to address these issues comprehensively. The changes are designed to be implemented sequentially, and progress will be tracked here.

### Implementation Plan

- [x] **1. Fix Best Action Selection**: In `findBestAction`, change the selection criterion from the most-visited child to the child with the highest average reward. Use visit count as a tie-breaker.

- [x] **2. Complete Transposition Table Integration**:
  - [x] In `expand()`, call `transpositionTable->store()` for newly created nodes.
  - [x] When `lookup()` finds an existing node, implement logic to merge statistics and redirect the parent's child pointer to the found node to prevent duplicate tree branches.

- [x] **3. Implement Cycle Detection and Penalties**:
  - [x] In `simulate()`, maintain a `std::unordered_set` of visited state hashes. If a hash is repeated, apply a penalty and terminate the rollout.
  - [x] In `evaluateTerminalState`, add a penalty proportional to the number of repeated cell visits during the game.

- [x] **4. Re-balance Reward Shaping**:
  - [x] Scale down the large, immediate rewards for pellet collection in `simulate()` and `evaluateTerminalState`.
  - [x] Introduce a reward for exploring unique cells to encourage less repetitive movement.

- [x] **5. Correct `UseItem` Logic in Simulation**: Remove the `continue` statement in the `UseItem` case within `selectSimulationAction` to allow the score to be evaluated correctly.

- [x] **6. Test and Verify**:
  - [x] Create a unit test with a simple maze to confirm the bot can solve a non-trivial path.
  - [x] Run benchmarks to measure the reduction in repeated moves and the increase in pellet collection rate.

## Consequences

### Positive:
- The bot's performance will dramatically improve, leading to more intelligent and effective gameplay.
- The bot will no longer get stuck in simple loops and will actively seek out pellets.
- The MCTS implementation will be more robust, correct, and aligned with established best practices.
- The search will be more efficient due to the fully functional transposition table.

### Negative:
- The changes involve modifications to the core MCTS engine, which carries a risk of introducing new bugs if not implemented carefully.
- The re-balancing of rewards may require some tuning to find the optimal values.
