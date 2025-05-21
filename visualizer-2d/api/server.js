const express = require('express');
const cors = require('cors');
const fs = require('fs').promises;
const path = require('path');

const app = express();
const PORT = process.env.PORT || 5008;

// Middleware
app.use(cors());
app.use(express.json());

// Path to game logs directory
const LOGS_DIR = path.join(__dirname, '..', 'logs');

// Ensure logs directory exists
async function ensureLogsDir() {
  try {
    await fs.mkdir(LOGS_DIR, { recursive: true });
    console.log(`Logs directory created/verified at: ${LOGS_DIR}`);
  } catch (error) {
    console.error('Failed to create logs directory:', error);
  }
}

// Get list of all available games
app.get('/api/games', async (req, res) => {
  try {
    await ensureLogsDir();
    console.log("API: Fetching games list");
    
    // Get all game directories
    const gameRuns = await fs.readdir(LOGS_DIR);
    console.log(`Found ${gameRuns.length} potential game runs in ${LOGS_DIR}`);
    
    // Build the list of available games with metadata
    const games = [];
    for (const runId of gameRuns) {
      try {
        const runPath = path.join(LOGS_DIR, runId);
        const stat = await fs.stat(runPath);
        
        if (!stat.isDirectory()) continue;
        
        const files = await fs.readdir(runPath);
        const logFiles = files.filter(file => file.endsWith('.json'));
        
        if (logFiles.length > 0) {
          // Get metadata from the first log file to provide game information
          try {
            const firstLogPath = path.join(runPath, logFiles[0]);
            const logContent = await fs.readFile(firstLogPath, 'utf8');
            const gameData = JSON.parse(logContent);
            
            games.push({
              id: runId,
              name: `Game ${runId}`,
              date: stat.mtime,
              playerCount: (gameData.animals || gameData.Animals || []).length,
              tickCount: logFiles.length
            });
          } catch (metadataError) {
            // If we can't read metadata, still include the game with basic info
            games.push({
              id: runId,
              name: `Game ${runId}`,
              date: stat.mtime,
              tickCount: logFiles.length
            });
          }
        }
      } catch (runError) {
        console.error(`Error processing run ${runId}:`, runError);
      }
    }
    
    // Sort games by date (newest first)
    games.sort((a, b) => new Date(b.date) - new Date(a.date));
    
    console.log(`Returning ${games.length} game entries`);
    res.json({ games });
  } catch (error) {
    console.error('Error getting games list:', error);
    res.status(500).json({ error: 'Failed to get games list' });
  }
});

// Get all data for a specific game
app.get('/api/games/:gameId', async (req, res) => {
  try {
    const { gameId } = req.params;
    const gameDir = path.join(LOGS_DIR, gameId);
    console.log(`API: Fetching game data for: ${gameId}`);
    
    try {
      await fs.access(gameDir);
    } catch {
      return res.status(404).json({ error: 'Game not found' });
    }
    
    const files = await fs.readdir(gameDir);
    const logFiles = files.filter(file => file.endsWith('.json')).sort();
    
    if (logFiles.length === 0) {
      return res.status(404).json({ error: 'No log files found for this game' });
    }
    
    // Load all game states
    const worldStates = [];
    for (const logFile of logFiles) {
      try {
        const logPath = path.join(gameDir, logFile);
        const logContent = await fs.readFile(logPath, 'utf8');
        const gameState = JSON.parse(logContent);
        worldStates.push(gameState);
      } catch (logError) {
        console.error(`Error reading log file ${logFile}:`, logError);
      }
    }
    
    console.log(`Sending game with ${worldStates.length} states`);
    // Return entire game history for replay
    res.json({ gameId, worldStates });
  } catch (error) {
    console.error('Error getting game data:', error);
    res.status(500).json({ error: 'Failed to get game data' });
  }
});

// Legacy endpoints for backward compatibility
app.get('/api/log_runs', async (req, res) => {
  try {
    await ensureLogsDir();
    const runs = await fs.readdir(LOGS_DIR);
    
    // Filter to only include directories
    const validRuns = [];
    for (const run of runs) {
      try {
        const runPath = path.join(LOGS_DIR, run);
        const stat = await fs.stat(runPath);
        if (stat.isDirectory()) {
          validRuns.push(run);
        }
      } catch (err) {
        console.error(`Error checking run ${run}:`, err);
      }
    }
    
    res.json({ runs: validRuns });
  } catch (error) {
    console.error('Error getting runs:', error);
    res.status(500).json({ error: 'Failed to get run list' });
  }
});

app.get('/api/logs/:runId', async (req, res) => {
  try {
    const { runId } = req.params;
    const runDir = path.join(LOGS_DIR, runId);
    
    try {
      await fs.access(runDir);
    } catch {
      return res.status(404).json({ error: 'Run not found' });
    }
    
    const files = await fs.readdir(runDir);
    const logFiles = files.filter(file => file.endsWith('.json'));
    
    res.json({ log_files: logFiles });
  } catch (error) {
    console.error('Error getting log files:', error);
    res.status(500).json({ error: 'Failed to get log files' });
  }
});

app.get('/api/logs/:runId/:logFile', async (req, res) => {
  try {
    const { runId, logFile } = req.params;
    const logPath = path.join(LOGS_DIR, runId, logFile);
    
    try {
      await fs.access(logPath);
    } catch {
      return res.status(404).json({ error: 'Log file not found' });
    }
    
    const logContent = await fs.readFile(logPath, 'utf8');
    const gameState = JSON.parse(logContent);
    
    res.json(gameState);
  } catch (error) {
    console.error('Error reading log file:', error);
    res.status(500).json({ error: 'Failed to read log file' });
  }
});

// Leaderboard stats endpoint
app.get('/api/leaderboard_stats', async (req, res) => {
  // This would typically connect to a database or file with aggregated stats
  // For now, we'll return sample data
  const sampleData = [
    { nickname: "Speedy", wins: 15, secondPlaces: 8, gamesPlayed: 30 },
    { nickname: "Hunter", wins: 12, secondPlaces: 10, gamesPlayed: 28 },
    { nickname: "Sneaky", wins: 10, secondPlaces: 12, gamesPlayed: 25 },
    { nickname: "Explorer", wins: 8, secondPlaces: 9, gamesPlayed: 22 },
    { nickname: "RandomBot", wins: 5, secondPlaces: 7, gamesPlayed: 18 }
  ];
  
  res.json(sampleData);
});

// Start server
app.listen(PORT, () => {
  console.log(`API server running on port ${PORT}`);
  ensureLogsDir();
});
