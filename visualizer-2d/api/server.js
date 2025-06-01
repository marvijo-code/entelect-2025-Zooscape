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

// Cache for frequently accessed data
const cache = new Map();
const CACHE_TTL = 5 * 60 * 1000; // 5 minutes

// Helper function to get cached data or compute it
function getCachedData(key, computeFn, ttl = CACHE_TTL) {
  const cached = cache.get(key);
  if (cached && Date.now() - cached.timestamp < ttl) {
    return Promise.resolve(cached.data);
  }
  
  return computeFn().then(data => {
    cache.set(key, { data, timestamp: Date.now() });
    return data;
  });
}

// Ensure logs directory exists
async function ensureLogsDir() {
  try {
    await fs.mkdir(LOGS_DIR, { recursive: true });
    console.log(`Logs directory created/verified at: ${LOGS_DIR}`);
  } catch (error) {
    console.error('Failed to create logs directory:', error);
  }
}

// Optimized function to read and parse JSON files
async function readJsonFile(filePath) {
  try {
    const content = await fs.readFile(filePath, 'utf8');
    return JSON.parse(content);
  } catch (error) {
    console.error(`Error reading JSON file ${filePath}:`, error);
    return null;
  }
}

// Optimized function to sort log files numerically
function sortLogFiles(files) {
  return files
    .filter(file => file.endsWith('.json'))
    .sort((a, b) => {
      const numA = parseInt(a.replace(/\.json$/, ''), 10);
      const numB = parseInt(b.replace(/\.json$/, ''), 10);
      
      if (isNaN(numA) && isNaN(numB)) return 0;
      if (isNaN(numA)) return 1;
      if (isNaN(numB)) return -1;
      
      return numA - numB;
    });
}

// Get list of all available games
app.get('/api/games', async (req, res) => {
  try {
    await ensureLogsDir();
    console.log("API: Fetching games list");
    
    const games = await getCachedData('games-list', async () => {
      const gameRuns = await fs.readdir(LOGS_DIR);
      console.log(`Found ${gameRuns.length} potential game runs in ${LOGS_DIR}`);
      
      const games = [];
      
      // Process games in parallel for better performance
      const gamePromises = gameRuns.map(async (runId) => {
        try {
          const runPath = path.join(LOGS_DIR, runId);
          const stat = await fs.stat(runPath);
          
          if (!stat.isDirectory()) return null;
          
          const files = await fs.readdir(runPath);
          const logFiles = sortLogFiles(files);
          
          if (logFiles.length === 0) return null;
          
          // Get metadata from the first log file
          const firstLogPath = path.join(runPath, logFiles[0]);
          const gameData = await readJsonFile(firstLogPath);
          
          if (!gameData) return null;
          
          return {
            id: runId,
            name: `Game ${runId}`,
            date: stat.mtime,
            playerCount: (gameData.animals || gameData.Animals || []).length,
            tickCount: logFiles.length
          };
        } catch (runError) {
          console.error(`Error processing run ${runId}:`, runError);
          return null;
        }
      });
      
      const results = await Promise.all(gamePromises);
      return results
        .filter(game => game !== null)
        .sort((a, b) => new Date(b.date) - new Date(a.date));
    });
    
    console.log(`Returning ${games.length} game entries`);
    res.json({ games });
  } catch (error) {
    console.error('Error getting games list:', error);
    res.status(500).json({ error: 'Failed to get games list' });
  }
});

// Get all data for a specific game with optimized loading
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
    
    const worldStates = await getCachedData(`game-${gameId}`, async () => {
      const files = await fs.readdir(gameDir);
      const logFiles = sortLogFiles(files);
      
      if (logFiles.length === 0) {
        throw new Error('No log files found for this game');
      }
      
      console.log(`Loading ${logFiles.length} game states for ${gameId}`);
      
      // Load all game states in parallel with limited concurrency
      const BATCH_SIZE = 10; // Process 10 files at a time to avoid overwhelming the system
      const worldStates = [];
      
      for (let i = 0; i < logFiles.length; i += BATCH_SIZE) {
        const batch = logFiles.slice(i, i + BATCH_SIZE);
        const batchPromises = batch.map(async (logFile) => {
          const logPath = path.join(gameDir, logFile);
          const gameState = await readJsonFile(logPath);
          
          if (!gameState) return null;
          
          // Add filePath property for tracking
          gameState.filePath = `${gameId}/${logFile}`;
          
          // Normalize property case for frontend compatibility
          const normalized = { ...gameState };
          
          if (gameState.Animals && !gameState.animals) {
            normalized.animals = gameState.Animals;
          } else if (gameState.animals && !gameState.Animals) {
            normalized.Animals = gameState.animals;
          }
          
          if (gameState.Cells && !gameState.cells) {
            normalized.cells = gameState.Cells;
          } else if (gameState.cells && !gameState.Cells) {
            normalized.Cells = gameState.cells;
          }
          
          if (gameState.Zookeepers && !gameState.zookeepers) {
            normalized.zookeepers = gameState.Zookeepers;
          } else if (gameState.zookeepers && !gameState.Zookeepers) {
            normalized.Zookeepers = gameState.zookeepers;
          }
          
          return normalized;
        });
        
        const batchResults = await Promise.all(batchPromises);
        worldStates.push(...batchResults.filter(state => state !== null));
        
        // Log progress for large games
        if (logFiles.length > 50) {
          console.log(`Loaded batch ${Math.floor(i / BATCH_SIZE) + 1}/${Math.ceil(logFiles.length / BATCH_SIZE)} for game ${gameId}`);
        }
      }
      
      console.log(`Successfully loaded ${worldStates.length} states for game ${gameId}`);
      return worldStates;
    }, 10 * 60 * 1000); // Cache for 10 minutes for game data
    
    // Return entire game history for replay
    res.json({ gameId, worldStates });
  } catch (error) {
    console.error('Error getting game data:', error);
    res.status(500).json({ error: 'Failed to get game data' });
  }
});

// Endpoint to get a specific game state by tick for replay
app.get('/api/replay/:gameId/:tick', async (req, res) => {
  try {
    const { gameId, tick } = req.params;
    const logFileName = `${tick}.json`;
    const logFilePath = path.join(LOGS_DIR, gameId, logFileName);
    console.log(`API: Fetching replay state for game: ${gameId}, tick: ${tick}`);

    const gameState = await getCachedData(`replay-${gameId}-${tick}`, async () => {
      try {
        await fs.access(logFilePath); // Check if file exists
      } catch {
        // This error will be caught by the outer try-catch, leading to a 404 if specific handling is needed
        // Or, we can throw a custom error to be more specific if getCachedData doesn't propagate it well
        console.warn(`Log file not found: ${logFilePath}`);
        return null; // Indicate not found to be handled by the caller
      }

      const state = await readJsonFile(logFilePath);
      if (!state) {
        return null; // Indicate error or not found
      }

      // Normalize property case for frontend compatibility
      const normalized = { ...state };
      if (state.Animals && !state.animals) normalized.animals = state.Animals;
      else if (state.animals && !state.Animals) normalized.Animals = state.animals;
      
      if (state.Cells && !state.cells) normalized.cells = state.Cells;
      else if (state.cells && !state.Cells) normalized.Cells = state.cells;
      
      if (state.Zookeepers && !state.zookeepers) normalized.zookeepers = state.Zookeepers;
      else if (state.zookeepers && !state.Zookeepers) normalized.Zookeepers = state.zookeepers;
      
      // Add filePath for consistency if needed, though less relevant for single tick
      normalized.filePath = `${gameId}/${logFileName}`;
      normalized.gameId = gameId;
      normalized.tick = parseInt(tick, 10);

      return normalized;
    });

    if (!gameState) {
      return res.status(404).json({ error: 'Game state for the specified tick not found' });
    }

    res.json(gameState);

  } catch (error) {
    console.error(`Error getting replay state for game ${req.params.gameId}, tick ${req.params.tick}:`, error);
    res.status(500).json({ error: 'Failed to get replay game state' });
  }
});

// Legacy endpoints for backward compatibility
app.get('/api/log_runs', async (req, res) => {
  try {
    await ensureLogsDir();
    const runs = await fs.readdir(LOGS_DIR);
    
    // Filter to only include directories in parallel
    const validRuns = [];
    const runPromises = runs.map(async (run) => {
      try {
        const runPath = path.join(LOGS_DIR, run);
        const stat = await fs.stat(runPath);
        if (stat.isDirectory()) {
          return run;
        }
      } catch (err) {
        console.error(`Error checking run ${run}:`, err);
      }
      return null;
    });
    
    const results = await Promise.all(runPromises);
    validRuns.push(...results.filter(run => run !== null));
    
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
    const logFiles = sortLogFiles(files);
    
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
    
    const gameState = await readJsonFile(logPath);
    if (!gameState) {
      return res.status(500).json({ error: 'Failed to parse log file' });
    }
    
    // Add filePath property for tracking
    gameState.filePath = `${runId}/${logFile}`;
    
    // Normalize state properties for frontend compatibility
    const normalized = { ...gameState };
    
    if (gameState.Animals && !gameState.animals) {
      normalized.animals = gameState.Animals;
    } else if (gameState.animals && !gameState.Animals) {
      normalized.Animals = gameState.animals;
    }
    
    if (gameState.Cells && !gameState.cells) {
      normalized.cells = gameState.Cells;
    } else if (gameState.cells && !gameState.Cells) {
      normalized.Cells = gameState.cells;
    }
    
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

// Optimized leaderboard stats endpoint
app.get('/api/leaderboard_stats', async (req, res) => {
  try {
    await ensureLogsDir();
    console.log("API: Calculating leaderboard stats from logs");
    
    const leaderboard = await getCachedData('leaderboard-stats', async () => {
      const gameRuns = await fs.readdir(LOGS_DIR);
      const botStats = {};
      
      // Process games in parallel
      const gamePromises = gameRuns.map(async (runId) => {
        try {
          const runPath = path.join(LOGS_DIR, runId);
          const stat = await fs.stat(runPath);
          
          if (!stat.isDirectory()) return null;
          
          const files = await fs.readdir(runPath);
          const logFiles = sortLogFiles(files);
          
          if (logFiles.length === 0) return null;
          
          // Get the last log file (final state of the game)
          const finalLogPath = path.join(runPath, logFiles[logFiles.length - 1]);
          const gameState = await readJsonFile(finalLogPath);
          
          if (!gameState) return null;
          
          const animals = gameState.animals || gameState.Animals || [];
          if (animals.length === 0) return null;
          
          // Sort animals by score to determine winners
          const sortedAnimals = [...animals].sort((a, b) => {
            const scoreA = a.score !== undefined ? a.score : a.Score;
            const scoreB = b.score !== undefined ? b.score : b.Score;
            return scoreB - scoreA;
          });
          
          return sortedAnimals;
        } catch (error) {
          console.error(`Error processing game run ${runId}:`, error);
          return null;
        }
      });
      
      const gameResults = await Promise.all(gamePromises);
      
      // Process results and build stats
      gameResults.filter(result => result !== null).forEach(sortedAnimals => {
        sortedAnimals.forEach((animal, index) => {
          const animalId = animal.id !== undefined ? animal.id : animal.Id;
          const nickname = animal.nickname !== undefined ? animal.nickname : 
                          animal.Nickname !== undefined ? animal.Nickname : 
                          `Bot-${animalId}`;
          
          if (!botStats[nickname]) {
            botStats[nickname] = {
              nickname,
              id: animalId,
              wins: 0,
              secondPlaces: 0,
              gamesPlayed: 0
            };
          }
          
          botStats[nickname].gamesPlayed++;
          
          if (index === 0) {
            botStats[nickname].wins++;
          }
          
          if (index === 1) {
            botStats[nickname].secondPlaces++;
          }
        });
      });
      
      // Convert to array and sort by wins
      return Object.values(botStats).sort((a, b) => 
        b.wins - a.wins || b.secondPlaces - a.secondPlaces || b.gamesPlayed - a.gamesPlayed
      );
    });
    
    console.log(`Returning leaderboard with ${leaderboard.length} bots`);
    res.json(leaderboard);
  } catch (error) {
    console.error('Error calculating leaderboard stats:', error);
    res.status(500).json({ error: 'Failed to calculate leaderboard statistics' });
  }
});

// Clear cache endpoint for development
app.post('/api/clear-cache', (req, res) => {
  cache.clear();
  console.log('Cache cleared');
  res.json({ message: 'Cache cleared successfully' });
});

// Start server
app.listen(PORT, () => {
  console.log(`API server running on port ${PORT}`);
  ensureLogsDir();
});
