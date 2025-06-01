import React, { useState, useEffect, useCallback, useRef, useMemo } from 'react';
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
  const [activeTabIndex, setActiveTabIndex] = useState(0); // 0 for Leaderboard by default
  
  const animalColors = useMemo(() => ['blue', 'green', 'purple', 'cyan', 'magenta', 'yellow', 'lime', 'teal'], []);
  const playbackTimerRef = useRef(null);

  // Memoize current game state to avoid unnecessary re-renders
  const currentGameState = useMemo(() => {
    if (allGameStates.length === 0 || currentDisplayIndex < 0 || currentDisplayIndex >= allGameStates.length) {
      return null;
    }
    return allGameStates[currentDisplayIndex];
  }, [allGameStates, currentDisplayIndex]);

  // Memoize grid data to prevent unnecessary Grid re-renders
  const gridData = useMemo(() => {
    if (!currentGameState) {
      return { cells: [], animals: [], zookeepers: [] };
    }
    
    return {
      cells: currentGameState.cells || currentGameState.Cells || [],
      animals: currentGameState.animals || currentGameState.Animals || [],
      zookeepers: currentGameState.zookeepers || currentGameState.Zookeepers || []
    };
  }, [currentGameState]);

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
        
        // Add file paths if they don't exist
        const gameId = payload.gameId || "replay";
        const statesWithFilePaths = history.map((state, index) => {
          if (!state.filePath) {
            return {
              ...state,
              filePath: `${gameId}/${index + 1}.json`, // Files are named 1.json, 2.json, etc.
              gameId: gameId
            };
          }
          return state;
        });
        
        setAllGameStates(statesWithFilePaths);
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

  // Effect 4: Auto-play animation when isPlaying is true - optimized with useCallback
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

  // Memoize game control handlers to prevent unnecessary re-renders
  const handleRewind = useCallback(() => {
    setCurrentDisplayIndex(prev => Math.max(0, prev - 1));
  }, []);
  
  const handlePlayPause = useCallback(() => {
    setIsPlaying(prev => !prev);
  }, []);
  
  const handleForward = useCallback(() => {
    setCurrentDisplayIndex(prev => Math.min(allGameStates.length - 1, prev + 1));
  }, [allGameStates.length]);
  
  const handleSetFrame = useCallback((frameIndex) => {
    if (frameIndex >= 0 && frameIndex < allGameStates.length) {
      setCurrentDisplayIndex(frameIndex);
    }
  }, [allGameStates.length]);
  
  const handleSpeedChange = useCallback((newSpeed) => {
    setPlaybackSpeed(newSpeed);
  }, []);
  
  const handleRestart = useCallback(() => {
    setCurrentDisplayIndex(0);
    setIsPlaying(true);
  }, []);
  
  const handleExitReplay = useCallback(() => {
    setShowReplayMode(false);
    setIsReplaying(false);
    setAllGameStates([]);
    setCurrentDisplayIndex(0);
    setGameInitialized(false);
    setIsGameOver(false);
    setAnimalColorMap({});
    setError(null);
  }, []);
  
  const handleEnterReplayMode = useCallback(() => {
    setShowReplayMode(true);
    setIsReplaying(true);
    
    // Stop any existing SignalR connection when entering replay mode
    if (connection && connection.state === signalR.HubConnectionState.Connected) {
      connection.stop().then(() => {
        console.log("SignalR connection stopped for replay mode");
        setIsConnected(false);
      }).catch(err => {
        console.error("Error stopping SignalR connection for replay mode:", err);
      });
    }
    
    // Clear any existing game state
    setAllGameStates([]);
    setCurrentDisplayIndex(0);
    setGameInitialized(false);
    setIsGameOver(false);
    setAnimalColorMap({});
    setError(null);
    
    // Switch to Game Selector tab
    setActiveTabIndex(1);
  }, [connection]);

  const handleGameSelected = useCallback((gameData) => {
    console.log("Game selected for replay:", gameData);
    setError(null);
    
    try {
      // Expecting gameData to have worldStates array
      if (gameData.worldStates && Array.isArray(gameData.worldStates)) {
        console.log(`Loading replay with ${gameData.worldStates.length} states`);
        
        setAllGameStates(gameData.worldStates);
        setCurrentDisplayIndex(0);
        setGameInitialized(true);
        setIsPlaying(true);
        setIsGameOver(false);
        
        // Map initial animals to colors from first state
        const firstState = gameData.worldStates[0];
        if (firstState) {
          const animalsArray = firstState.animals || firstState.Animals || [];
          setAnimalColorMap(animalsArray.reduce((map, a, idx) => {
            const animalKey = a.id || a.Id;
            if (animalKey) {
              map[animalKey] = animalColors[idx % animalColors.length];
            }
            return map;
          }, {}));
        }
        
        console.log("Replay game loaded successfully");
      } else {
        console.error("Invalid game data format for replay:", gameData);
        setError("Failed to load replay: Invalid game data format");
      }
    } catch (e) {
      console.error("Error loading replay game:", e);
      setError(`Failed to load replay: ${e.message}`);
    }
  }, [animalColors]);

  const handleTabChange = useCallback((tabIndex) => {
    setActiveTabIndex(tabIndex);
  }, []);

  // Memoize tab content to prevent unnecessary re-renders
  const tabContent = useMemo(() => {
    switch (activeTabIndex) {
      case 0:
        return (
          <Leaderboard 
            data={leaderboardData}
            loading={leaderboardLoading}
            statusMessage={leaderboardStatusMessage}
            onRefresh={fetchAggregateLeaderboardData}
          />
        );
      case 1:
        return (
          <GameSelector 
            onGameSelected={handleGameSelected}
            apiBaseUrl={API_BASE_URL}
          />
        );
      case 2:
        return (
          <ConnectionDebugger 
            connection={connection}
            isConnected={isConnected}
            hubUrl={HUB_URL}
          />
        );
      default:
        return null;
    }
  }, [activeTabIndex, leaderboardData, leaderboardLoading, leaderboardStatusMessage, fetchAggregateLeaderboardData, handleGameSelected, connection, isConnected]);

  // Memoize current tick display
  const currentTick = useMemo(() => {
    if (!currentGameState) return 0;
    return currentGameState.tick !== undefined ? currentGameState.tick : currentGameState.Tick || 0;
  }, [currentGameState]);

  return (
    <div className="app-container">
      <div className="split-layout">
        {/* Left Panel - Grid Only */}
        <div className="left-panel">
          <div className="grid-content">
            <Grid 
              cells={gridData.cells}
              animals={gridData.animals}
              zookeepers={gridData.zookeepers}
              colorMap={animalColorMap}
              showDetails={isReplaying}
            />
          </div>
        </div>
        
        {/* Right Panel - All Controls */}
        <div className="right-panel">
          {/* App Header */}
          <div className="app-header">
            <h1>Zooscape 2D Visualizer</h1>
            <div className="mode-buttons">
              {!showReplayMode ? (
                <button 
                  onClick={handleEnterReplayMode}
                  className="mode-button replay-button"
                >
                  📼 Replay Mode
                </button>
              ) : (
                <button 
                  onClick={handleExitReplay}
                  className="mode-button live-button"
                >
                  🔴 Live Mode
                </button>
              )}
            </div>
          </div>
          
          {/* Grid Header - Tick Info and Game Status */}
          <div className="grid-header">
            <div className="tick-info">
              <span className="tick-label">Tick:</span>
              <span className="tick-value">{currentTick}</span>
              {isGameOver && <span className="game-over-indicator">🏁 Game Over</span>}
            </div>
          </div>
          
          {/* Playback Controls - Only show when game is initialized */}
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
                onExitReplay={showReplayMode ? handleExitReplay : null}
              />
            </div>
          )}
          
          {/* Error Message */}
          {error && (
            <div className="error-message">
              <strong>Error:</strong> {error}
            </div>
          )}
          
          {/* Connection Status - Only show in live mode */}
          {!showReplayMode && (
            <div className="connection-status">
              <span className={`status-indicator ${isConnected ? 'connected' : 'disconnected'}`}>
                {isConnected ? '🟢 Connected' : '🔴 Disconnected'}
              </span>
            </div>
          )}
          
          {/* Tabs Header */}
          <div className="tabs-header">
            <TabsContainer 
              activeTabIndex={activeTabIndex}
              onTabChange={handleTabChange}
              showReplayMode={showReplayMode}
            />
          </div>
          
          {/* Right Panel Content */}
          <div className="right-panel-content">
            {tabContent}
          </div>
        </div>
      </div>
    </div>
  );
};

export default App;
