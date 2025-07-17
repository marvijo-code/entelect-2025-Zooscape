---
description: "Automated Weight Optimisation & Path-Decision Analysis"
---

# Automated Heuristic Weight Optimisation & Path-Decision Analysis

This guide collects all tooling and workflows for automatically tuning heuristic weights and identifying long-term path-decision errors.

## 1. Performance-Pattern Detection

> **Goal** – Find heuristics that exceed their time budget (> 20 ms each) so you can refactor or down-weight them.

```powershell
# Profile heuristic performance in a game-state
.\inspect-game-state.ps1 -GameStateFile "<state.json>" -BotNickname "StaticHeuro" -ProfilePerformance
```

**Recommended budget** (fits the 200 ms total limit)
| Category | Target ms |
|-----------|-----------|
| Core movement heuristics | ~100 |
| Safety heuristics | ~50 |
| Path-planning heuristics | ~30 |
| Bonus / penalty heuristics | ~20 |

---

## 2. Weight-Conflict Detection

```powershell
# Detect heuristics that work against each other
 dotnet run --project tools\HeuristicConflictAnalyzer -- "logs\<dir>" "StaticHeuro" --analyze-conflicts
```
Outputs a report of conflicting-weight pairs and suggests adjustments.

---

## 3. Automated Weight Tuning

```powershell
# Generate suggested JSON patches for heuristic-weights.json
 dotnet run --project tools\AutoWeightTuner -- "logs\<dir>" "StaticHeuro" --suggest-weights
```
Apply the generated JSON patch to `heuristic-weights.json`, then **restart the API** and rerun your targeted functional tests.

---

## 4. Long-Term Path-Decision Error Detection

### 4.1 Path-Efficiency Analysis

```powershell
# Detect inefficient movement patterns over a log sequence
 dotnet run --project tools\PathEfficiencyAnalyzer -- "logs\<dir>" "StaticHeuro" --detect-inefficiencies
```
Flags circling behaviour, ignored clusters, unnecessarily long routes, etc.

### 4.2 Cluster-Targeting Analysis

```powershell
# Evaluate pellet-cluster decisions
 dotnet run --project tools\ClusterTargetingAnalyzer -- "logs\<dir>" "StaticHeuro" --analyze-clusters
```
Key metrics: cluster-completion rate, abandonment patterns, optimal-vs-actual cluster ranking.

### 4.3 Automated Test Generation for Path Errors

```powershell
# Generate functional tests for detected path errors
 .\generate-path-error-tests.ps1 -LogDirectory "logs\<dir>" -BotNickname "StaticHeuro" -ErrorType "cluster_abandonment"
```
`ErrorType` options:
* `cluster_abandonment` – Bot switches cluster mid-collection.
* `inefficient_pathing` – Suboptimal routes.
* `pellet_prioritization` – Wrong pellet-value assessment.

---

## 5. Continuous Improvement Loop

1. **Detect patterns** with tools above.
2. **Adjust weights** manually or via `AutoWeightTuner`.
3. **Restart API** and rerun focused tests.
4. **Commit only when tests pass** and performance budgets are met.
