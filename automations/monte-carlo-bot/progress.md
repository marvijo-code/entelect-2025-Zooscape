# Monte Carlo Bot Progress

This log is append-only. Each entry should capture:
- The attempt or change that was tried
- The measurable progress or regression against the active anchor
- Whether the attempt was promoted, rejected, or left pending

## Active Anchor

- 2026-04-04 08:02 +02:00: `output/headtohead/20260404_075313_MonteCarloBot_vs_NeuralNetBot` is the active verified anchor. MonteCarlo went `5-0` on seeds `101-105` with average score `104247.4`, average captures `0.2`, and average score diff `+52644` versus NeuralNet at `51603.4` / `3.2`.

## Attempts

- 2026-04-06 11:49 +02:00: Patched the benchmark lane rather than bot logic. `tools/run-neuralnet-headtohead.ps1` now accepts `-StartGameTimeout`, and `output/MonteCarloBotSandbox2/run-headtohead.ps1` no longer depends on unsupported `Start-Process -Environment`, now records captures, and writes `summary.json`. Result: the current MonteCarlo candidate still went `5-0` versus NeuralNet on seeds `101-105`, but with lower average score (`99848.8`) and higher captures (`0.6`) than the anchor, so the run was rejected and no new best tag was created.
- 2026-04-06 12:51 +02:00: Fixed the visible-terminal issue by keeping the sandbox runner on hidden child-process launch semantics and rebuilt `Bots/ClingyHeuroBot2` so the patched `WeightManager` is actually exercised in the sandbox lane. Result: a clean isolated canary on seed `102` at base port `5600` finished `112018 / 0 captures` for MonteCarlo versus `108068 / 0` for NeuralNet, but the full five-seed rerun `output/headtohead/20260406_124333_MonteCarloBot_vs_NeuralNetBot` only went `3-2` with average score `115901.2` and captures `0.0`. The capture metric improved, but losses on seeds `101` and `102` kept the attempt below the active `5-0` anchor, so it was not promoted and no new `monte-carlo-bot-best-*` tag was created.
- 2026-04-06 13:44 +02:00: Benchmarked the committed MonteCarlo sandbox lane against `StaticHeuro` on seeds `101-105` with `tools/run-neuralnet-headtohead.ps1 -CandidateProject output/MonteCarloBotSandbox2/MonteCarloBotSandbox.csproj -CandidateNickname MonteCarloBot -OpponentBot StaticHeuro -StartGameTimeout 60 -BasePort 5700`. Result: MonteCarlo swept `5-0` with average score `99230.4` and average captures `0.6` versus StaticHeuro at `71648.8` / `1.4`, finishing ahead on both score and captures. The harness recorded this as the new best `MonteCarloBot` vs `StaticHeuro` lane and created git tag `best-montecarlobot-vs-staticheuro-20260406-134413`.
