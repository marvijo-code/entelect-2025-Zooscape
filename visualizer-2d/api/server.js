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
    const logFiles = files
      .filter(file => file.endsWith('.json'))
      .sort((a, b) => {
        // Extract numeric part of filename (e.g., '11.json' -> 11)
        const numA = parseInt(a.replace(/\.json$/, ''), 10);
        const numB = parseInt(b.replace(/\.json$/, ''), 10);
        
        // Handle NaN cases and ensure proper numeric sorting
        if (isNaN(numA) && isNaN(numB)) return 0;
        if (isNaN(numA)) return 1;
        if (isNaN(numB)) return -1;
        
        return numA - numB; // Sort numerically
      });
    
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
        
        // Add filePath property to each state for easier tracking
        gameState.filePath = `${gameId}/${logFile}`;
        
        // Log first state's properties for debugging
        if (worldStates.length === 0) {
          console.log(`First game state properties: ${Object.keys(gameState).join(', ')}`);
          console.log(`Sample cell properties: ${Object.keys(gameState.Cells ? gameState.Cells[0] : gameState.cells ? gameState.cells[0] : {}).join(', ')}`);
          console.log(`Sample animal properties: ${Object.keys(gameState.Animals ? gameState.Animals[0] : gameState.animals ? gameState.animals[0] : {}).join(', ')}`);
        }
        
        worldStates.push(gameState);
      } catch (logError) {
        console.error(`Error reading log file ${logFile}:`, logError);
      }
    }
    
    console.log(`Sending game with ${worldStates.length} states`);
    
    // Log first few and last few log files to verify sorting
    if (logFiles.length > 0) {
      const sampleFiles = [
        ...logFiles.slice(0, Math.min(5, logFiles.length)), 
        ...(logFiles.length > 10 ? ['...'] : []),
        ...(logFiles.length > 10 ? logFiles.slice(-5) : [])
      ];
      console.log(`Log files (sorted sample): ${sampleFiles.join(', ')}`);
    }
    
    // Ensure property case consistency for frontend compatibility
    const normalizedWorldStates = worldStates.map(state => {
      // Create a new object to hold the normalized state
      const normalized = { ...state };
      
      // Ensure animals array exists with proper casing
      if (state.Animals && !state.animals) {
        normalized.animals = state.Animals;
      } else if (state.animals && !state.Animals) {
        normalized.Animals = state.animals;
      }
      
      // Ensure cells array exists with proper casing
      if (state.Cells && !state.cells) {
        normalized.cells = state.Cells;
      } else if (state.cells && !state.Cells) {
        normalized.Cells = state.cells;
      }
      
      // Ensure zookeepers array exists with proper casing
      if (state.Zookeepers && !state.zookeepers) {
        normalized.zookeepers = state.Zookeepers;
      } else if (state.zookeepers && !state.Zookeepers) {
        normalized.Zookeepers = state.zookeepers;
      }
      
      return normalized;
    });
    
    // Return entire game history for replay with normalized data
    res.json({ gameId, worldStates: normalizedWorldStates });
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
    
    // Add filePath property for tracking
    gameState.filePath = `${runId}/${logFile}`;
    
    // Normalize state properties for frontend compatibility
    const normalized = { ...gameState };
    
    // Ensure animals array exists with proper casing
    if (gameState.Animals && !gameState.animals) {
      normalized.animals = gameState.Animals;
    } else if (gameState.animals && !gameState.Animals) {
      normalized.Animals = gameState.animals;
    }
    
    // Ensure cells array exists with proper casing
    if (gameState.Cells && !gameState.cells) {
      normalized.cells = gameState.Cells;
    } else if (gameState.cells && !gameState.Cells) {
      normalized.Cells = gameState.cells;
    }
    
    // Ensure zookeepers array exists with proper casing
    if (gameState.Zookeepers && !gameState.zookeepers) {
      normalized.zookeepers = gameState.Zookeepers;
    } else if (gameState.zookeepers && !gameState.Zookeepers) {
      normalized.Zookeepers = gameState.zookeepers;
    }
    
    res.json(normalized);
  } catch (error) {
    console.error('Error reading log file:', error);
    res.status(500).json({ error: 'Failed to read log file' });
  }
});

// Leaderboard stats endpoint
app.get('/api/leaderboard_stats', async (req, res) => {
  try {
    await ensureLogsDir();
    console.log("API: Calculating leaderboard stats from logs");
    
    // Get all game directories
    const gameRuns = await fs.readdir(LOGS_DIR);
    
    // Stats tracker for each bot
    const botStats = {};
    
    // Process each game run
    for (const runId of gameRuns) {
      try {
        const runPath = path.join(LOGS_DIR, runId);
        const stat = await fs.stat(runPath);
        
        if (!stat.isDirectory()) continue;
        
        const files = await fs.readdir(runPath);
        const logFiles = files
          .filter(file => file.endsWith('.json'))
          .sort((a, b) => {
            // Extract numeric part of filename (e.g., '11.json' -> 11)
            const numA = parseInt(a.replace(/\.json$/, ''), 10);
            const numB = parseInt(b.replace(/\.json$/, ''), 10);
            
            // Handle NaN cases and ensure proper numeric sorting
            if (isNaN(numA) && isNaN(numB)) return 0;
            if (isNaN(numA)) return 1;
            if (isNaN(numB)) return -1;
            
            return numA - numB; // Sort numerically
          });
        
        if (logFiles.length === 0) continue;
        
        // Get the last log file (final state of the game)
        const finalLogPath = path.join(runPath, logFiles[logFiles.length - 1]);
        const logContent = await fs.readFile(finalLogPath, 'utf8');
        const gameState = JSON.parse(logContent);
        
        // Get animals array (handle both lowercase and uppercase property names)
        const animals = gameState.animals || gameState.Animals || [];
        
        if (animals.length === 0) continue;
        
        // Sort animals by score to determine winners
        const sortedAnimals = [...animals].sort((a, b) => {
          const scoreA = a.score !== undefined ? a.score : a.Score;
          const scoreB = b.score !== undefined ? b.score : b.Score;
          return scoreB - scoreA;
        });
        
        // Record stats for each animal/bot
        sortedAnimals.forEach((animal, index) => {
          const animalId = animal.id !== undefined ? animal.id : animal.Id;
          const nickname = animal.nickname !== undefined ? animal.nickname : 
                          animal.Nickname !== undefined ? animal.Nickname : 
                          `Bot-${animalId}`;
          
          // Initialize bot stats if not exists
          if (!botStats[nickname]) {
            botStats[nickname] = {
              nickname,
              id: animalId,
              wins: 0,
              secondPlaces: 0,
              gamesPlayed: 0
            };
          }
          
          // Record game participation
          botStats[nickname].gamesPlayed++;
          
          // Record win (1st place)
          if (index === 0) {
            botStats[nickname].wins++;
          }
          
          // Record 2nd place
          if (index === 1) {
            botStats[nickname].secondPlaces++;
          }
        });
      } catch (error) {
        console.error(`Error processing game run ${runId}:`, error);
      }
    }
    
    // Convert to array and sort by wins (descending)
    const leaderboard = Object.values(botStats).sort((a, b) => 
      b.wins - a.wins || b.secondPlaces - a.secondPlaces || b.gamesPlayed - a.gamesPlayed
    );
    
    console.log(`Returning leaderboard with ${leaderboard.length} bots`);
    res.json(leaderboard);
  } catch (error) {
    console.error('Error calculating leaderboard stats:', error);
    res.status(500).json({ error: 'Failed to calculate leaderboard statistics' });
  }
});

// Start server
app.listen(PORT, () => {
  console.log(`API server running on port ${PORT}`);
  ensureLogsDir();
});
