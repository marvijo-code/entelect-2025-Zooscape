import React, { useState, useEffect, useCallback, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import Grid from './components/Grid.jsx';
import Leaderboard from './components/Leaderboard.jsx';
import GameSelector from './components/GameSelector.jsx';
import PlaybackControls from './components/PlaybackControls.jsx';
import TabsContainer from './components/TabsContainer.jsx';
import ConnectionDebugger from './components/ConnectionDebugger.jsx';
import './App.css';
import './styles/ConnectionDebugger.css';

const HUB_URL = "http://localhost:5000/bothub";
const API_BASE_URL = 'http://localhost:5008/api'; // API server running on port 5008

const App = () => {
  const [connection, setConnection] = useState(null);
  const [allGameStates, setAllGameStates] = useState([]);
  const [currentDisplayIndex, setCurrentDisplayIndex] = useState(0);
  const [isPlaying, setIsPlaying] = useState(true);
  const [gameInitialized, setGameInitialized] = useState(false);
  const [isGameOver, setIsGameOver] = useState(false);
  const [error, setError] = useState(null);
  const [isConnected, setIsConnected] = useState(false);
  const [isReplaying, setIsReplaying] = useState(false);
  const [animalColorMap, setAnimalColorMap] = useState({});
  const [leaderboardData, setLeaderboardData] = useState([]);
  const [leaderboardLoading, setLeaderboardLoading] = useState(false);
  const [leaderboardStatusMessage, setLeaderboardStatusMessage] = useState('');
  const [playbackSpeed, setPlaybackSpeed] = useState(1.0);
  const [showReplayMode, setShowReplayMode] = useState(false);
  
  const animalColors = ['blue', 'green', 'purple', 'cyan', 'magenta', 'yellow', 'lime', 'teal'];
  const playbackTimerRef = useRef(null);

  const fetchAggregateLeaderboardData = useCallback(async () => {
    setLeaderboardLoading(true);
    setLeaderboardStatusMessage('Fetching leaderboard stats...');
    setLeaderboardData([]); // Clear previous data

    try {
      const response = await fetch(`${API_BASE_URL}/leaderboard_stats`);
      if (!response.ok) {
        const errorData = await response.text();
        console.error(`API Error! Status: ${response.status} - Failed to fetch leaderboard stats. Server says: ${errorData}`);
        setLeaderboardStatusMessage('Failed to load leaderboard stats. API error.');
        setLeaderboardData([]);
      } else {
        const data = await response.json();
        setLeaderboardData(data);
        setLeaderboardStatusMessage(data.length > 0 ? 'Leaderboard loaded.' : 'No data for leaderboard.');
      }
    } catch (error) {
      console.error('Network error or failed to parse response from /api/leaderboard_stats:', error);
      setLeaderboardStatusMessage('Failed to load leaderboard stats. Network error or bad response.');
      setLeaderboardData([]);
    }
    setLeaderboardLoading(false);
  }, []);
    
  useEffect(() => {
    fetchAggregateLeaderboardData();
  }, [fetchAggregateLeaderboardData]);

  // Effect 1: Create and store connection object. Stop it on component unmount.
  useEffect(() => {
    // Only create connection if we're not in replay mode
    if (!showReplayMode) {
      const newConnection = new signalR.HubConnectionBuilder()
        .withUrl(HUB_URL)
        .withAutomaticReconnect()
        .build();
      setConnection(newConnection);

      return () => { // Cleanup for component unmount
        console.log("App unmounting, stopping SignalR connection.");
        if (newConnection &&
          (newConnection.state === signalR.HubConnectionState.Connected ||
            newConnection.state === signalR.HubConnectionState.Connecting ||
            newConnection.state === signalR.HubConnectionState.Reconnecting)) {
          newConnection.stop().catch(err => console.error("Error stopping connection on unmount", err));
        } else if (newConnection) {
          console.log("App unmounting, connection already stopped or not started. State:", newConnection.state);
        }
      };
    }
  }, [showReplayMode]);

  // Effect 2: Start the connection when it's created and in Disconnected state.
  // This effect also handles registering the visualiser once connected.
  useEffect(() => {
    if (connection && connection.state === signalR.HubConnectionState.Disconnected && !showReplayMode) {
      connection.start()
        .then(() => {
          console.log('SignalR Connected!');
          setIsConnected(true);
          setError(null); // Clear previous errors on successful connect
          return connection.invoke("RegisterVisualiser"); // Chain the promise
        })
        .then(() => {
          console.log("Visualiser registered");
        })
        .catch(e => {
          console.error('Connection or Registration failed: ', e);
          setIsConnected(false);
          setError(`SignalR Connection/Registration Failed: ${e.message}. Check server at ${HUB_URL}.`);
        });
    }
  }, [connection, showReplayMode]);

  const initializeGameHandler = useCallback((data) => {
    console.log("StartGame received:", data);
    setError(null);
    try {
      const payload = typeof data === 'string' ? JSON.parse(data) : data;
      
      // Debug information
      console.log("StartGame payload keys:", Object.keys(payload));
      
      // If StartGame contains full history, use that
      const history = payload.WorldStates || payload.worldStates;
      if (Array.isArray(history) && history.length > 0) {
        console.log(`StartGame has history array with ${history.length} states`);
        console.log("Sample state keys:", Object.keys(history[0] || {}));
        
        setAllGameStates(history);
        setCurrentDisplayIndex(0);
        setGameInitialized(true);
        setIsPlaying(true);
        setIsGameOver(false);
        
        // Map initial animals to colors
        if (history[0]?.animals && Array.isArray(history[0].animals)) {
          setAnimalColorMap(history[0].animals.reduce((map, a, idx) => {
            const animalKey = a.id || a.Id;
            if (animalKey) {
              map[animalKey] = animalColors[idx % animalColors.length];
            }
            return map;
          }, {}));
        } else if (history[0]?.Animals && Array.isArray(history[0].Animals)) {
          setAnimalColorMap(history[0].Animals.reduce((map, a, idx) => {
            const animalKey = a.id || a.Id;
            if (animalKey) {
              map[animalKey] = animalColors[idx % animalColors.length];
            }
            return map;
          }, {}));
        }
        
        console.log(`Game initialized with history (${history.length} states).`);
        return;
      }

      // Check for single GameState format with Cells/Animals/Zookeepers
      const hasCells = Array.isArray(payload.cells) || Array.isArray(payload.Cells);
      const hasAnimals = Array.isArray(payload.animals) || Array.isArray(payload.Animals);
      const hasZookeepers = Array.isArray(payload.zookeepers) || Array.isArray(payload.Zookeepers);
      
      console.log("Payload format check:", { hasCells, hasAnimals, hasZookeepers });
      
      // Expecting a flat payload: { tick: ..., cells: ..., animals: ..., zookeepers: ... }
      if (hasCells && hasAnimals && hasZookeepers) {
        console.log("Using single GameState format");
        
        // If payload is a single GameState object
        setAllGameStates([payload]); // Store as an array with one state
        setCurrentDisplayIndex(0); // Or payload.tick if you want to use server's tick as index
        setGameInitialized(true);
        setIsPlaying(true);
        setIsGameOver(false);
        
        // Get the animals array from either lowercase or uppercase property
        const animalsArray = payload.animals || payload.Animals || [];
        
        // Map initial animals to colors
        setAnimalColorMap(animalsArray.reduce((map, a, idx) => {
          const animalKey = a.id || a.Id;
          if (animalKey) {
            map[animalKey] = animalColors[idx % animalColors.length];
          }
          return map;
        }, {}));
        
        console.log("Game initialized with single GameState data.");
      } else {
        console.error("StartGame: Invalid payload structure. Expected worldStates array or {cells, animals, zookeepers}", payload);
        setError("Failed to initialize game: Invalid data format from server.");
      }
    } catch (e) {
      console.error("Error parsing StartGame data:", e, "Raw data:", data);
      setError(`Failed to initialize game: ${e.message}`);
    }
  }, [animalColors]);

  const tickStateChangedHandler = useCallback((data) => {
    setError(null);
    try {
      const newGameState = typeof data === 'string' ? JSON.parse(data) : data;
      
      console.log("GameTick received:", { 
        tick: newGameState.tick || newGameState.Tick,
        hasCells: Array.isArray(newGameState.cells) || Array.isArray(newGameState.Cells),
        hasAnimals: Array.isArray(newGameState.animals) || Array.isArray(newGameState.Animals),
        hasZookeepers: Array.isArray(newGameState.zookeepers) || Array.isArray(newGameState.Zookeepers)
      });

      // Check for valid game state format with either lowercase or uppercase property names
      const hasTick = newGameState.tick !== undefined || newGameState.Tick !== undefined;
      const hasCells = Array.isArray(newGameState.cells) || Array.isArray(newGameState.Cells);
      const hasAnimals = Array.isArray(newGameState.animals) || Array.isArray(newGameState.Animals);
      const hasZookeepers = Array.isArray(newGameState.zookeepers) || Array.isArray(newGameState.Zookeepers);
      
      // Expecting a flat payload: { timeStamp: ..., tick: ..., cells: ..., animals: ..., zookeepers: ... }
      if (hasTick && hasCells && hasAnimals && hasZookeepers) {
        if (!gameInitialized) {
          setGameInitialized(true); // Initialize on first valid GameState
          console.log("Game initialized with first GameState data.");
          setIsPlaying(true);
          setIsGameOver(false);
          
          // Get the animals array
          const animalsArray = newGameState.animals || newGameState.Animals || [];
          
          // Map animals on first tick
          setAnimalColorMap(prevMap => {
            const newMap = { ...prevMap };
            let colorIndex = Object.keys(newMap).length;
            animalsArray.forEach(animal => {
              const animalKey = animal.id || animal.Id;
              if (animalKey && !newMap[animalKey]) { // Only add if new
                newMap[animalKey] = animalColors[colorIndex % animalColors.length];
                colorIndex++;
              }
            });
            return newMap;
          });
        }

        setAllGameStates(prevStates => {
          // Get the tick value from either lowercase or uppercase property
          const tickValue = newGameState.tick !== undefined ? newGameState.tick : newGameState.Tick;
          
          // Add new state and auto-advance only if playing and at latest frame
          if (prevStates.length > 0) {
            const latestTick = prevStates[prevStates.length - 1].tick !== undefined 
              ? prevStates[prevStates.length - 1].tick 
              : prevStates[prevStates.length - 1].Tick;
            
            if (tickValue <= latestTick) {
              console.log(`Skipping already processed tick: ${tickValue}`);
              return prevStates;
            }
          }
          
          const updatedStates = [...prevStates, newGameState];
          if (isPlaying) {
            setCurrentDisplayIndex(updatedStates.length - 1);
          }
          return updatedStates;
        });
      } else {
        console.error("Invalid GameState structure. Missing required properties:", {
          hasTick, hasCells, hasAnimals, hasZookeepers
        });
      }
    } catch (e) {
      console.error("Error processing game state:", e, "Raw data:", data);
      setError(`Failed to process game state: ${e.message}`);
    }
  }, [animalColors, gameInitialized, isPlaying]);

  // Effect 3: Register event handlers on the connection when it's created
  useEffect(() => {
    if (connection && !showReplayMode) {
      console.log("Registering SignalR event handlers...");
      
      // Register the event handlers with the connection
      connection.on("StartGame", (data) => {
        console.log("StartGame event received");
        initializeGameHandler(data);
      });
      
      connection.on("GameTick", (data) => {
        console.log("GameTick event received");
        tickStateChangedHandler(data);
      });
      
      // Also try alternative event names that might be used by the server
      connection.on("GameState", (data) => {
        console.log("GameState event received (alternative to GameTick)");
        tickStateChangedHandler(data);
      });
      
      connection.on("Tick", (data) => {
        console.log("Tick event received (alternative to GameTick)");
        tickStateChangedHandler(data);
      });
      
      connection.on("GameOver", () => {
        console.log("GameOver received");
        setIsGameOver(true);
        setIsPlaying(false);
      });
      
      // Add connection state change handlers
      connection.onreconnecting((error) => {
        console.log("SignalR reconnecting:", error);
        setIsConnected(false);
      });
      
      connection.onreconnected((connectionId) => {
        console.log("SignalR reconnected with ID:", connectionId);
        setIsConnected(true);
        connection.invoke("RegisterVisualiser").catch(err => {
          console.error("Error re-registering visualizer after reconnect:", err);
        });
      });
      
      connection.onclose((error) => {
        console.log("SignalR connection closed:", error);
        setIsConnected(false);
      });

      // Remove event handlers when component unmounts
      return () => {
        console.log("Removing SignalR event handlers...");
        connection.off("StartGame");
        connection.off("GameTick");
        connection.off("GameState");
        connection.off("Tick");
        connection.off("GameOver");
        connection.off("reconnecting");
        connection.off("reconnected");
        connection.off("close");
      };
    }
  }, [connection, initializeGameHandler, tickStateChangedHandler, showReplayMode]);

  // Effect 4: Auto-play animation when isPlaying is true
  useEffect(() => {
    clearTimeout(playbackTimerRef.current);
    
    if (isPlaying && allGameStates.length > 0 && currentDisplayIndex < allGameStates.length - 1) {
      // Calculate the timer interval, faster at higher speeds
      const timerInterval = 125 / playbackSpeed; // 125ms / speed = interval in ms
      
      playbackTimerRef.current = setTimeout(() => {
        setCurrentDisplayIndex(prev => {
          const nextIndex = prev + 1;
          // If we reach the end, pause playback
          if (nextIndex >= allGameStates.length) {
            setIsPlaying(false);
            return prev;
          }
          return nextIndex;
        });
      }, timerInterval);
    }
    
    return () => clearTimeout(playbackTimerRef.current);
  }, [isPlaying, currentDisplayIndex, allGameStates.length, playbackSpeed]);

  // Game controls handlers
  const handleRewind = () => {
    setCurrentDisplayIndex(prev => Math.max(0, prev - 1));
  };
  
  const handlePlayPause = () => {
    setIsPlaying(prev => !prev);
  };
  
  const handleForward = () => {
    setCurrentDisplayIndex(prev => Math.min(allGameStates.length - 1, prev + 1));
  };
  
  const handleSetFrame = (frameIndex) => {
    if (frameIndex >= 0 && frameIndex < allGameStates.length) {
      setCurrentDisplayIndex(frameIndex);
    }
  };
  
  const handleSpeedChange = (newSpeed) => {
    setPlaybackSpeed(newSpeed);
  };
  
  const handleRestart = () => {
    setCurrentDisplayIndex(0);
    setIsPlaying(true);
  };
  
  const handleExitReplay = () => {
    setShowReplayMode(false);
    setAllGameStates([]);
    setCurrentDisplayIndex(0);
    setGameInitialized(false);
    setIsReplaying(false);
  };

  // Generate tabs for our SPA
  const getTabs = () => {
    // 1. Grid View Tab
    const gridTab = {
      label: 'Grid View',
      content: (
        <div className="grid-tab">
          <div className="grid-view">
            <div className="grid-header">
              <div>
                {gameInitialized ? (
                  <span>Tick: {currentDisplayIndex + 1}/{allGameStates.length}</span>
                ) : (
                  <span>Waiting for game to start...</span>
                )}
              </div>
              {isGameOver && <span className="game-status">Game Over</span>}
            </div>
            <div className="grid-content">
              {gameInitialized && allGameStates.length > 0 && (
                <Grid
                  cells={allGameStates[currentDisplayIndex].cells || allGameStates[currentDisplayIndex].Cells || []}
                  animals={allGameStates[currentDisplayIndex].animals || allGameStates[currentDisplayIndex].Animals || []}
                  zookeepers={allGameStates[currentDisplayIndex].zookeepers || allGameStates[currentDisplayIndex].Zookeepers || []}
                  colorMap={animalColorMap}
                />
              )}
              {!gameInitialized && (
                <div className="waiting-message">
                  {error ? (
                    <p>{error}</p>
                  ) : (
                    showReplayMode ? (
                      <p>Select a game to replay from the Game Selector tab</p>
                    ) : (
                      <p>Waiting for game to start...</p>
                    )
                  )}
                </div>
              )}
            </div>
          </div>
          {gameInitialized && (
            <div className="playback-controls-container">
              <PlaybackControls
                currentFrame={currentDisplayIndex}
                totalFrames={allGameStates.length}
                isPlaying={isPlaying}
                onPlayPause={handlePlayPause}
                onRewind={handleRewind}
                onForward={handleForward}
                onSetFrame={handleSetFrame}
                onSpeedChange={handleSpeedChange}
                onRestart={handleRestart}
                onExitReplay={handleExitReplay}
              />
            </div>
          )}
        </div>
      )
    };

    // 2. Leaderboard Tab
    const leaderboardTab = {
      label: 'Leaderboard',
      content: (
        <div className="leaderboard-tab">
          <div className="leaderboard-container scroll-content">
            <Leaderboard
              animals={gameInitialized && allGameStates.length > 0 ? allGameStates[currentDisplayIndex].animals : []}
              leaderboardData={leaderboardData}
              loading={leaderboardLoading}
              statusMessage={leaderboardStatusMessage}
              colorMap={animalColorMap}
            />
          </div>
        </div>
      )
    };

    // 3. Game Selector Tab (only in replay mode)
    const gameSelectorTab = {
      label: 'Game Selector',
      content: (
        <div className="game-selector-tab">
          <div className="selector-container scroll-content">
            <GameSelector 
              onGameSelected={handleGameSelected} 
              apiBaseUrl={API_BASE_URL}
            />
          </div>
        </div>
      )
    };

    // 4. Connection Debugger Tab (only in live mode)
    const connectionDebuggerTab = {
      label: 'Connection',
      content: (
        <div className="connection-debugger-tab">
          <div className="connection-container">
            <h2>Connection Status</h2>
            <ConnectionDebugger connection={connection} hubUrl={HUB_URL} />
          </div>
        </div>
      )
    };

    // Return tabs based on current mode
    if (showReplayMode) {
      return [gridTab, leaderboardTab, gameSelectorTab];
    }
    return [gridTab, leaderboardTab, connectionDebuggerTab];
  };

  const handleEnterReplayMode = () => {
    // Stop and disconnect from SignalR
    if (connection && connection.state === signalR.HubConnectionState.Connected) {
      connection.stop()
        .then(() => {
          console.log("Connection stopped for replay mode");
          setConnection(null);
          
          // Reset game state for replay
          setAllGameStates([]);
          setCurrentDisplayIndex(0);
          setGameInitialized(false);
          setIsGameOver(false);
          setError(null);
          setIsConnected(false);
          
          // Enter replay mode
          setShowReplayMode(true);
          setIsReplaying(true);
        })
        .catch(err => {
          console.error("Error stopping connection for replay mode:", err);
          setError(`Failed to enter replay mode: ${err.message}`);
        });
    } else {
      // If not connected, just enter replay mode
      setShowReplayMode(true);
      setIsReplaying(true);
      setGameInitialized(false);
      setAllGameStates([]);
      setCurrentDisplayIndex(0);
    }
  };

  const handleGameSelected = (gameData) => {
    console.log("Game selected for replay:", gameData);
    initializeGameHandler(gameData);
  };

  return (
    <div className="app-container">
      <header className="app-header">
        <h1>Zooscape 2D Visualizer</h1>
        {!showReplayMode && !isConnected && (
          <button className="mode-switch-button" onClick={handleEnterReplayMode}>
            Switch to Replay Mode
          </button>
        )}
        {!showReplayMode && isConnected && (
          <button className="mode-switch-button" onClick={handleEnterReplayMode}>
            Switch to Replay Mode
          </button>
        )}
        {showReplayMode && (
          <button className="mode-switch-button" onClick={handleExitReplay}>
            Exit Replay Mode
          </button>
        )}
      </header>
      
      {error && <div className="error-message">{error}</div>}
      
      {!showReplayMode && !isConnected && !error && (
        <div className="connection-status">
          <p>Connecting to server at {HUB_URL}...</p>
        </div>
      )}
      
      <div className="app-content">
        <TabsContainer tabs={getTabs()} />
      </div>
    </div>
  );
};

export default App;
