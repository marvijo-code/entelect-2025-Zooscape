# Zooscape Visualizer PRD

## Purpose

The visualizer provides a headless-testable web UI for inspecting Zooscape matches, replay logs, leaderboard aggregates, and functional test scenarios without opening extra desktop terminals.

## Primary User Flows

### Leaderboard

- Show aggregate leaderboard statistics from the ASP.NET `FunctionalTests` API.
- Load from `GET /api/Leaderboard/stats`.
- Present a useful empty/error state when the API is unavailable.

### Paste JSON

- Accept either raw game-state JSON or an absolute file path to a JSON/log file inside the repo tree.
- Load file-path requests through `GET /api/Replay/file/load-json?path=...`.
- Render the supplied state in the main grid without requiring a live game.

### Replay Game Selector

- List replayable logged matches from `GET /api/Replay/games`.
- Show match metadata including date, player count, tick count, and seed.
- Load replay ticks from `GET /api/Replay/{gameId}/{tick}`.
- Show the selected replay name and seed in the active replay header.

### Test Runner

- Load test definitions from `GET /api/Test/definitions`.
- Run saved tests, run all tests, inspect source game states, and create new tests from the currently viewed state.
- Reuse the same configured API base URL as the other screens so settings and env changes apply consistently.

### Settings

- Show effective hub/API configuration and startup preferences.
- Persist user overrides in browser local storage.

### Live Mode

- Connect to the SignalR hub configured by `VITE_HUB_URL`.
- Show connection state and live ticks without replay controls.

## Runtime Architecture

- `http://localhost:5008` is the primary ASP.NET API host for replay, leaderboard, and test data.
- The Vite frontend runs separately and calls the ASP.NET API directly.
- `visualizer-2d/api/server.cjs` is optional helper infrastructure and must not occupy port `5008`; its default fallback port is `5009`.

## Headless Validation Requirement

- All screens must be exercisable from a headless browser session.
- Screenshot capture is required for validation runs so regressions can be reviewed without a visible desktop session.

## Current Verified Behavior

- Replay listings expose match seeds.
- The active replay header shows the selected seed when available.
- Frontend API consumers now share one configured API base URL instead of mixing settings-based and raw env-based endpoints.
- The default helper API port no longer conflicts with the ASP.NET API port used by `Leaderboard`, `Replay`, and `Test` screens.
