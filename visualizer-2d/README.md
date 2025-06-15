# Zooscape 2D Visualizer

A React-based web application for visualizing Zooscape game sessions in real-time or replay mode.

## Features

- **Live Mode**: Connect to live game sessions via SignalR
- **Replay Mode**: Browse and replay saved game logs
- **Configurable Defaults**: Set default mode and tab preferences
- **Settings Interface**: Configure preferences through the web UI
- **Environment Variables**: System-wide configuration via .env files

## Quick Start

1. Install dependencies:

   ```bash
   npm install
   ```

2. Copy the environment configuration:

   ```bash
   cp .env.example .env
   ```

3. Start the development server:
   ```bash
   npm run dev
   ```

## Configuration

### Environment Variables

The application supports configuration through environment variables in a `.env` file:

```bash
# Default mode configuration
VITE_DEFAULT_REPLAY_MODE=true          # Start in Replay Mode (true) or Live Mode (false)
VITE_DEFAULT_ACTIVE_TAB=2              # Default active tab (0-4)

# Connection URLs
VITE_HUB_URL=http://localhost:5000/bothub      # SignalR hub URL for live mode
VITE_API_BASE_URL=http://localhost:5008/api    # API server URL for replay data

# Debug settings
VITE_DEBUG_LOGS=false                  # Enable debug logging
```

### Tab Indices

- **0**: Leaderboard - View aggregate statistics
- **1**: JSON Paste Loader - Load game states from JSON
- **2**: Game Selector (Replay) / Connection (Live) - Browse games or manage connection
- **3**: Test Runner - Create and run tests (Replay mode only)
- **4**: Settings - Configure application preferences

### Frontend Settings

Users can override environment defaults through the Settings tab:

1. Click the "⚙️ Settings" tab
2. Modify default startup configuration
3. Save settings (stored in browser localStorage)
4. Settings persist between sessions

### Configuration Priority

1. **User Preferences** (localStorage) - Highest priority
2. **Environment Variables** (.env file) - Fallback
3. **Application Defaults** - Final fallback

## Usage

### Live Mode

1. Ensure the game server is running on the configured hub URL
2. Start the visualizer in Live Mode
3. The application will automatically connect and display real-time game data

### Replay Mode

1. Place game log files in the `logs/` directory
2. Start the visualizer in Replay Mode
3. Use the Game Selector tab to browse and select games
4. Control playback with the playback controls

### Settings Management

- **Save Settings**: Apply current configuration as user preferences
- **Reset to Defaults**: Clear user preferences and use environment/application defaults
- **Environment Override**: User settings override .env file values (indicated in UI)

## Development

### Project Structure

```
src/
├── components/          # React components
│   ├── Settings.jsx    # Settings configuration UI
│   └── ...
├── config/             # Configuration management
│   └── appConfig.js    # Configuration utilities
├── styles/             # CSS stylesheets
│   ├── Settings.css    # Settings component styles
│   └── ...
└── ...
```

### Configuration System

The configuration system (`src/config/appConfig.js`) provides:

- Environment variable parsing
- User preference management (localStorage)
- Configuration merging and validation
- Tab index validation based on current mode

### Adding New Configuration Options

1. Add the environment variable to `.env` and `.env.example`
2. Update `getEnvConfig()` in `appConfig.js`
3. Add UI controls in `Settings.jsx`
4. Update validation logic if needed

## API Endpoints

- **Live Data**: SignalR connection to `VITE_HUB_URL`
- **Replay Data**: REST API at `VITE_API_BASE_URL`
  - `GET /replay/{gameId}/{tickNumber}` - Get specific tick data
  - `GET /leaderboard_stats` - Get aggregate statistics

## Browser Support

- Modern browsers with ES6+ support
- localStorage support required for settings persistence
- WebSocket support required for live mode

## Troubleshooting

### Connection Issues

1. Verify the game server is running
2. Check `VITE_HUB_URL` configuration
3. Review browser console for connection errors
4. Use the Connection tab to debug SignalR status

### Settings Not Persisting

1. Ensure localStorage is enabled in browser
2. Check browser privacy settings
3. Clear browser data and reconfigure if needed

### Invalid Tab States

The application automatically validates and corrects invalid tab configurations:

- Test Runner tab is hidden in Live Mode
- Settings tab is always available
- Invalid indices are corrected based on current mode

## Building for Production

```bash
npm run build
```

The built application will be in the `dist/` directory.

## License

[Add your license information here]
