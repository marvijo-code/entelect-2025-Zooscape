import React, { useState, useEffect, useCallback, useRef, useMemo } from 'react';
import { HubConnectionBuilder, LogLevel, HubConnectionState } from "@microsoft/signalr";
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
  const [showReplayMode, setShowReplayMode] = useState(true);
  const [activeTabIndex, setActiveTabIndex] = useState(1);
  const [liveTickQueue, setLiveTickQueue] = useState([]);
  const [isProcessingLiveTick, setIsProcessingLiveTick] = useState(false);
  const lastTickTimeRef = useRef(Date.now());
  const frameRateRef = useRef(60); // Target 60fps by default
  const tickProcessorRef = useRef(null);
  const pendingAnimationFrameRef = useRef(null);
  const [replayingGameName, setReplayingGameName] = useState(null);
  const [replayGameId, setReplayGameId] = useState(null);
  const [replayTickCount, setReplayTickCount] = useState(0);
  const [currentReplayTick, setCurrentReplayTick] = useState(0);
  const [isFetchingTick, setIsFetchingTick] = useState(false);
  const tickMetricsRef = useRef({
    receivedCount: 0,
    processedCount: 0,
    droppedCount: 0,
    lastProcessTime: 0,
    averageProcessTime: 0
  });
  const animalColors = useMemo(() => ['blue', 'green', 'purple', 'cyan', 'magenta', 'yellow', 'lime', 'teal'], []);
  const playbackTimerRef = useRef(null);
  const [processingQueue, setProcessingQueue] = useState([]);
  const processingTimeoutRef = useRef(null);

  const fetchAndDisplayReplayTick = useCallback(async (gameId, tickNumber) => {
    if (isFetchingTick) return;
    setIsFetchingTick(true);
    setError(null);
    console.log(`Fetching replay tick: ${gameId}, tick number: ${tickNumber}`);
    try {
      // API ticks are 1-based, internal (currentReplayTick) is 0-based
      const apiTickNumber = tickNumber + 1;
      const url = `${API_BASE_URL}/replay/${gameId}/${apiTickNumber}`;
      console.log(`Making API request to: ${url}`);
      
      const response = await fetch(url);
      console.log(`API response status: ${response.status}`);
      
      if (!response.ok) {
        const errorText = await response.text();
        console.error(`API error response: ${errorText}`);
        throw new Error(`Failed to fetch tick ${apiTickNumber} for game ${gameId}: ${response.status} ${errorText}`);
      }
      
      const tickData = await response.json();
      console.log(`Received tick data:`, tickData);
      console.log(`Tick data keys:`, Object.keys(tickData));
      
      // Specifically check for cells data
      if (tickData.cells) {
        console.log(`Found cells array with ${tickData.cells.length} cells`);
        if (tickData.cells.length > 0) {
          console.log(`Sample cell:`, tickData.cells[0]);
        }
      } else if (tickData.Cells) {
        console.log(`Found Cells array with ${tickData.Cells.length} cells`);
        if (tickData.Cells.length > 0) {
          console.log(`Sample Cell:`, tickData.Cells[0]);
        }
      } else {
        console.warn(`No cells or Cells property found in tick data. Available properties:`, Object.keys(tickData));
      }
      
      if (!tickData || typeof tickData.tick === 'undefined') {
        console.error(`Invalid tick data structure:`, tickData);
        throw new Error('Invalid tick data received');
      }

      console.log(`Setting game state for tick ${tickData.tick}`);
      setAllGameStates([tickData]); // Store only the current tick
      setCurrentDisplayIndex(0); // Always display the first (and only) state in allGameStates
      setCurrentReplayTick(tickNumber);

      // Update animal colors
      if (tickData.animals) {
        console.log(`Updating animal colors for ${tickData.animals.length} animals`);
        const newColors = {};
        tickData.animals.forEach(animal => {
          if (!animalColorMap[animal.id]) {
            newColors[animal.id] = animalColors[Math.floor(Math.random() * animalColors.length)];
          } else {
            newColors[animal.id] = animalColorMap[animal.id];
          }
        });
        setAnimalColorMap(prevColors => ({ ...prevColors, ...newColors }));
      }
      setError(null);
      console.log(`Successfully processed tick ${tickNumber} (API tick ${apiTickNumber})`);
    } catch (e) {
      console.error("Error fetching replay tick:", e);
      setError(`Failed to load tick ${tickNumber + 1}: ${e.message}`);
      setAllGameStates([]); // Clear states on error
    }
    setIsFetchingTick(false);
  }, [isFetchingTick, animalColors, animalColorMap, API_BASE_URL]);

  const handleGameSelected = useCallback(async (game) => { // game object from GameSelector now contains id, name, tickCount etc.
    if (!game || !game.id) {
      setError('Invalid game selected.');
      return;
    }
    console.log(`Game selected for replay:`, game); // More detailed logging
    console.log(`Game properties - ID: ${game.id}, Name: ${game.name}, TickCount: ${game.tickCount}`);
    
    setShowReplayMode(true);
    setIsReplaying(true);
    setIsPlaying(false); // Start paused
    setGameInitialized(true); // Game is considered initialized once a replay is selected
    setError(null);

    setReplayGameId(game.id);
    setReplayTickCount(game.tickCount || 0); // Ensure tickCount is a number
    setReplayingGameName(game.name || `Game ${game.id}`);
    setCurrentReplayTick(0); // Start from the first tick
    setAllGameStates([]); // Clear any previous game states
    
    // Fetch the first tick to start
    console.log(`About to fetch first tick for game ${game.id}`);
    try {
      await fetchAndDisplayReplayTick(game.id, 0);
      console.log(`Successfully loaded first tick for game ${game.id}`);
    } catch (error) {
      console.error(`Failed to load initial replay tick for game ${game.id}:`, error);
      setError(`Failed to load initial replay tick: ${error.message}`);
    }
  }, [fetchAndDisplayReplayTick]);

  // LEGACY: This is the old handleGameSelected that loads all states
  const handleGameSelected_Legacy = useCallback((gameData) => {
    console.log("Game selected for replay (LEGACY):", gameData);
    setError(null);
    setReplayingGameName(null);
    
    try {
      // Expecting gameData to have worldStates array and a displayName (from GameSelector)
      if (gameData.worldStates && Array.isArray(gameData.worldStates)) {
        console.log(`Loading replay with ${gameData.worldStates.length} states. Name: ${gameData.displayName} (LEGACY)`);
        
        setAllGameStates(gameData.worldStates);
        setCurrentDisplayIndex(0);
        setGameInitialized(true);
        setIsPlaying(true); // Original logic started playing immediately
        setIsGameOver(false);
        setReplayingGameName(gameData.displayName || 'Unnamed Replay');
        
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
        
        console.log("Replay game loaded successfully (LEGACY)");
      } else {
        console.error("Invalid game data format for replay (LEGACY):", gameData);
        setError("Failed to load replay: Invalid game data format (LEGACY)");
      }
    } catch (e) {
      console.error("Error loading replay game (LEGACY):", e);
      setError(`Failed to load replay (LEGACY): ${e.message}`);
    }
  }, [animalColors]);

  const handleTabChange = useCallback((tabIndex) => {
    setActiveTabIndex(tabIndex);
  }, []);

  const tickBufferRef = useRef([]);
  const tickProcessingTimerRef = useRef(null);
  const animationFrameRequestedRef = useRef(false);

  // Refs for state values to stabilize callbacks
  const gameInitializedRef = useRef(gameInitialized);
  const isProcessingLiveTickRef = useRef(isProcessingLiveTick);

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
      console.log("No currentGameState available for grid");
      return { cells: [], animals: [], zookeepers: [] };
    }
    
    console.log("Current game state keys:", Object.keys(currentGameState));
    console.log("Current game state structure:", currentGameState);
    
    const cells = currentGameState.cells || currentGameState.Cells || [];
    const animals = currentGameState.animals || currentGameState.Animals || [];
    const zookeepers = currentGameState.zookeepers || currentGameState.Zookeepers || [];
    
    console.log(`Grid data extracted - Cells: ${cells.length}, Animals: ${animals.length}, Zookeepers: ${zookeepers.length}`);
    
    if (cells.length === 0) {
      console.warn("No cells found in game state. Available properties:", Object.keys(currentGameState));
    } else {
      // Log a few sample cells to understand their structure
      console.log("Sample cells (first 5):", cells.slice(0, 5));
      
      // Check for different content types
      const contentTypes = {};
      cells.slice(0, 100).forEach(cell => { // Check first 100 cells for performance
        const content = cell.content !== undefined ? cell.content : cell.Content;
        contentTypes[content] = (contentTypes[content] || 0) + 1;
      });
      console.log("Cell content types found:", contentTypes);
    }
    
    return {
      cells,
      animals,
      zookeepers
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

  // Handler for game tick updates
  const tickStateChangedHandler = useCallback((data) => {
    setError(null);
    try {
      const newGameState = typeof data === 'string' ? JSON.parse(data) : data;
      
      if (!newGameState.tick && newGameState.tick !== 0) {
        throw new Error("Game state is missing tick number");
      }
      
      if (!gameInitializedRef.current) {
        setGameInitialized(true);
      }
      
      tickMetricsRef.current.receivedCount++;
      
      setLiveTickQueue(prevQueue => {
        if (prevQueue.length > 10) {
          const newQueue = [...prevQueue.slice(-5), newGameState];
          tickMetricsRef.current.droppedCount += (prevQueue.length - newQueue.length + 1);
          return newQueue;
        }
        return [...prevQueue, newGameState];
      });
      
      if (tickProcessorRef.current && !isProcessingLiveTickRef.current) {
        tickProcessorRef.current();
      }
      
    } catch (e) {
      setError(`Failed to process game update: ${e.message}`);
    }
  }, []);

  // Effect 1: Create and store connection object. Stop it on component unmount.
  useEffect(() => {
    let connectionInstance = null;

    if (!showReplayMode && HUB_URL) {
      console.log("Attempting to establish SignalR connection for live mode...");
      connectionInstance = new HubConnectionBuilder()
        .withUrl(HUB_URL)
        .withAutomaticReconnect([
          0, 
          1000, 
          5000, 
          10000 
        ])
        .configureLogging(LogLevel.Information)
        .build();

      setConnection(connectionInstance);

      connectionInstance.on("GameTick", tickStateChangedHandler);
      connectionInstance.on("GameState", tickStateChangedHandler);
      connectionInstance.on("Tick", tickStateChangedHandler);
      // Add GameOver handler if it's not already present or if it was removed
      connectionInstance.on("GameOver", () => {
        console.log("GameOver received via SignalR");
        setIsGameOver(true);
        setIsPlaying(false); // Stop playback on game over
      });

      connectionInstance.onreconnecting(error => {
        console.warn('SignalR reconnecting:', error);
        setIsConnected(false);
      });

      connectionInstance.onreconnected(connectionId => {
        console.log('SignalR reconnected with ID:', connectionId);
        setIsConnected(true);
        tickMetricsRef.current = {
          receivedCount: 0,
          processedCount: 0,
          droppedCount: 0,
          lastProcessTime: 0,
          averageProcessTime: 0
        };
        // Optionally, re-register visualiser if needed by your hub
        // connectionInstance.invoke("RegisterVisualiser").catch(err => console.error("Error re-registering visualizer", err));
      });

      connectionInstance.onclose(error => {
        console.error('SignalR connection closed:', error);
        setIsConnected(false);
        setError(error ? `Connection closed: ${error.message}` : 'Connection closed');
      });

      connectionInstance.start()
        .then(() => {
          console.log("SignalR Connected for live mode");
          setIsConnected(true);
          setError(null);
          // Optionally, register visualiser if needed by your hub
          // connectionInstance.invoke("RegisterVisualiser").catch(err => console.error("Error registering visualizer", err));
        })
        .catch((err) => {
          console.error("SignalR Connection Error: ", err);
          setIsConnected(false);
          setError(`Failed to connect to game server: ${err.message}`);
        });
    } else {
      // If in replay mode or no HUB_URL, ensure any existing connection is stopped and cleared
      if (connection) {
        console.log("In replay mode or HUB_URL not set, stopping existing SignalR connection.");
        connection.stop().catch(err => console.error("Error stopping connection on mode change/no HUB_URL", err));
        setConnection(null);
        setIsConnected(false);
      }
    }

    return () => {
      if (connectionInstance && connectionInstance.state === HubConnectionState.Connected) {
        console.log("Cleaning up SignalR connection (Effect 1 unmount or change)");
        connectionInstance.stop().catch(err => console.error("Error stopping connection during cleanup", err));
      }
    };
  }, [HUB_URL, tickStateChangedHandler, showReplayMode]); // Added showReplayMode to dependencies

  // Effect: Process live ticks at a controlled frame rate with advanced scheduling
  useEffect(() => {
    // Define the tick processor function that will be used in requestAnimationFrame
    const processTick = () => {
      if (liveTickQueue.length === 0 || showReplayMode) {
        setIsProcessingLiveTick(false);
        pendingAnimationFrameRef.current = null;
        return;
      }
      
      const processStart = performance.now();
      setLiveTickQueue(prevQueue => {
        if (prevQueue.length === 0) return prevQueue;
        
        // Track metrics
        tickMetricsRef.current.processedCount++;
        if (prevQueue.length > 1) {
          tickMetricsRef.current.droppedCount += (prevQueue.length - 1);
        }
        
        // Process the latest tick
        const latestState = prevQueue[prevQueue.length - 1];
        
        // Update the game state with the latest tick
        setAllGameStates(prev => [latestState]);
        setCurrentDisplayIndex(0);
        
        // Update animal colors if needed
        if (latestState.animals) {
          const newColors = {};
          latestState.animals.forEach(animal => {
            if (!animalColorMap[animal.id]) {
              newColors[animal.id] = animalColors[Math.floor(Math.random() * animalColors.length)];
            } else {
              newColors[animal.id] = animalColorMap[animal.id];
            }
          });
          setAnimalColorMap(prevColors => ({ ...prevColors, ...newColors }));
        }
        
        // Clear the queue
        return [];
      });
      
      // Update timing metrics
      const processEnd = performance.now();
      const processTime = processEnd - processStart;
      tickMetricsRef.current.lastProcessTime = processTime;
      tickMetricsRef.current.averageProcessTime = 
        (tickMetricsRef.current.averageProcessTime * 0.9) + (processTime * 0.1); // Weighted average
      
      lastTickTimeRef.current = Date.now();
      setIsProcessingLiveTick(false);
      pendingAnimationFrameRef.current = null;
      
      // If there are more ticks in the queue, schedule another frame
      if (liveTickQueue.length > 0) {
        scheduleNextFrame();
      }
    };
    
    // Function to schedule the next animation frame for processing
    const scheduleNextFrame = () => {
      if (pendingAnimationFrameRef.current === null && !showReplayMode) {
        pendingAnimationFrameRef.current = requestAnimationFrame(processTick);
      }
    };
    
    // Store the scheduler in a ref for cleanup
    tickProcessorRef.current = scheduleNextFrame;
    
    // Schedule processing if there are ticks and we're not already processing
    if (liveTickQueue.length > 0 && !isProcessingLiveTick && !showReplayMode && pendingAnimationFrameRef.current === null) {
      setIsProcessingLiveTick(true);
      scheduleNextFrame();
    }
    
    // Cleanup function
    return () => {
      if (pendingAnimationFrameRef.current !== null) {
        cancelAnimationFrame(pendingAnimationFrameRef.current);
        pendingAnimationFrameRef.current = null;
      }
    };
  }, [liveTickQueue, isProcessingLiveTick, showReplayMode, animalColorMap, animalColors]);
  
  // Log performance metrics periodically
  useEffect(() => {
    const metricsInterval = setInterval(() => {
      if (tickMetricsRef.current.processedCount > 0) {
        console.log('Tick processing metrics:', {
          received: tickMetricsRef.current.receivedCount,
          processed: tickMetricsRef.current.processedCount,
          dropped: tickMetricsRef.current.droppedCount,
          lastProcessTime: tickMetricsRef.current.lastProcessTime.toFixed(2) + 'ms',
          averageProcessTime: tickMetricsRef.current.averageProcessTime.toFixed(2) + 'ms',
          queueLength: liveTickQueue.length
        });
      }
    }, 5000); // Log every 5 seconds
    
    return () => clearInterval(metricsInterval);
  }, [liveTickQueue.length]);
  
  // Clean up any existing tick processing logic that's no longer needed
  useEffect(() => {
    return () => {
      if (tickProcessingTimerRef.current) {
        clearTimeout(tickProcessingTimerRef.current);
        tickProcessingTimerRef.current = null;
      }
    };
  }, []);

  // Effect 5: Auto-play animation when isPlaying is true - optimized with useCallback
  useEffect(() => {
    clearTimeout(playbackTimerRef.current);
    
    if (isPlaying && showReplayMode && replayGameId) {
      // Calculate the timer interval, faster at higher speeds
      const timerInterval = 125 / playbackSpeed; // 125ms / speed = interval in ms
      
      playbackTimerRef.current = setTimeout(async () => {
        // Calculate the next tick to fetch
        const nextTick = currentReplayTick + 1;
        
        // If we reach the end, pause playback
        if (nextTick >= replayTickCount) {
          setIsPlaying(false);
          return;
        }
        
        // Fetch the next tick
        try {
          await fetchAndDisplayReplayTick(replayGameId, nextTick);
        } catch (error) {
          console.error('Error fetching next tick during playback:', error);
          setIsPlaying(false); // Pause on error
          setError(`Playback error: ${error.message}`);
        }
      }, timerInterval);
    }
    
    return () => clearTimeout(playbackTimerRef.current);
  }, [isPlaying, playbackSpeed, currentReplayTick, replayTickCount, replayGameId, showReplayMode, fetchAndDisplayReplayTick]);

  // Memoize game control handlers to prevent unnecessary re-renders
  const handleRewind = useCallback(async () => {
    if (showReplayMode && replayGameId) {
      const prevTick = Math.max(0, currentReplayTick - 1);
      try {
        await fetchAndDisplayReplayTick(replayGameId, prevTick);
      } catch (error) {
        setError(`Failed to load previous tick: ${error.message}`);
      }
    } else {
      // Legacy behavior for non-replay mode
      setCurrentDisplayIndex(prev => Math.max(0, prev - 1));
    }
  }, [showReplayMode, replayGameId, currentReplayTick, fetchAndDisplayReplayTick]);
  
  const handlePlayPause = useCallback(() => {
    setIsPlaying(prev => !prev);
  }, []);
  
  const handleForward = useCallback(async () => {
    if (showReplayMode && replayGameId) {
      const nextTick = Math.min(replayTickCount - 1, currentReplayTick + 1);
      try {
        await fetchAndDisplayReplayTick(replayGameId, nextTick);
      } catch (error) {
        setError(`Failed to load next tick: ${error.message}`);
      }
    } else {
      // Legacy behavior for non-replay mode
      setCurrentDisplayIndex(prev => Math.min(allGameStates.length - 1, prev + 1));
    }
  }, [showReplayMode, replayGameId, currentReplayTick, replayTickCount, fetchAndDisplayReplayTick, allGameStates.length]);
  
  const handleSetFrame = useCallback(async (frameIndex) => {
    if (showReplayMode && replayGameId) {
      // In replay mode, frameIndex represents the tick number
      if (frameIndex >= 0 && frameIndex < replayTickCount) {
        try {
          await fetchAndDisplayReplayTick(replayGameId, frameIndex);
        } catch (error) {
          setError(`Failed to load tick ${frameIndex + 1}: ${error.message}`);
        }
      }
    } else {
      // Legacy behavior for non-replay mode
      if (frameIndex >= 0 && frameIndex < allGameStates.length) {
        setCurrentDisplayIndex(frameIndex);
      }
    }
  }, [showReplayMode, replayGameId, replayTickCount, fetchAndDisplayReplayTick, allGameStates.length]);
  
  const handleSpeedChange = useCallback((newSpeed) => {
    setPlaybackSpeed(newSpeed);
  }, []);
  
  const handleRestart = useCallback(async () => {
    if (showReplayMode && replayGameId) {
      try {
        await fetchAndDisplayReplayTick(replayGameId, 0);
        setIsPlaying(true);
      } catch (error) {
        setError(`Failed to restart replay: ${error.message}`);
      }
    } else {
      // Legacy behavior for non-replay mode
      setCurrentDisplayIndex(0);
      setIsPlaying(true);
    }
  }, [showReplayMode, replayGameId, fetchAndDisplayReplayTick]);
  
  const handleExitReplay = useCallback(() => {
    // Clear replay mode flags
    setShowReplayMode(false);
    setIsReplaying(false);
    setIsPlaying(false);
    
    // Clear replay data
    setAllGameStates([]);
    setCurrentDisplayIndex(0);
    setReplayGameId(null);
    setReplayTickCount(0);
    setCurrentReplayTick(0);
    setReplayingGameName(null);
    
    // Reset game state
    setGameInitialized(false);
    setIsGameOver(false);
    setAnimalColorMap({});
    setError(null);
    
    // Clear any pending timers
    if (playbackTimerRef.current) {
      clearTimeout(playbackTimerRef.current);
      playbackTimerRef.current = null;
    }
  }, []);
  
  const handleEnterReplayMode = useCallback(() => {
    setShowReplayMode(true);
    setIsReplaying(true);
    
    // Stop any existing SignalR connection when entering replay mode
    if (connection && connection.state === HubConnectionState.Connected) {
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
    setReplayingGameName(null);
  }, []);

  // Effect to process the queue
  useEffect(() => {
    const processBatch = () => {
      if (processingQueue.length > 0) {
        const batchSize = 10; // Process 10 states at a time
        const batchToProcess = processingQueue.slice(0, batchSize);
        const remainingInQueue = processingQueue.slice(batchSize);

        setAllGameStates(prevStates => [...prevStates, ...batchToProcess]);
        setProcessingQueue(remainingInQueue);

        if (remainingInQueue.length > 0) {
          // Schedule next batch processing
          processingTimeoutRef.current = setTimeout(processBatch, 100); // Adjust delay as needed
        } else {
          console.log("Finished processing all initial states from queue.");
        }
      }
    };

    if (processingQueue.length > 0 && processingTimeoutRef.current === null) { // ensure it only starts if not already running
      // Start processing if queue has items and no timeout is already set
      processingTimeoutRef.current = setTimeout(processBatch, 0); // Start immediately for the first batch
    }

    // Cleanup timeout on component unmount or if queue becomes empty
    return () => {
      if (processingTimeoutRef.current) {
        clearTimeout(processingTimeoutRef.current);
        processingTimeoutRef.current = null;
      }
    };
  }, [processingQueue]);

  // Effect to keep refs updated
  useEffect(() => {
    gameInitializedRef.current = gameInitialized;
  }, [gameInitialized]);

  useEffect(() => {
    isProcessingLiveTickRef.current = isProcessingLiveTick;
  }, [isProcessingLiveTick]);

  // Memoize current tick display
  const currentTick = useMemo(() => {
    if (!currentGameState) return 0;
    return currentGameState.tick !== undefined ? currentGameState.tick : currentGameState.Tick || 0;
  }, [currentGameState]);

  // Memoize tab content based on active tab
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
              <span className="tick-value">
                {showReplayMode ? (
                  <>
                    {isFetchingTick ? (
                      <span className="loading-indicator">Loading...</span>
                    ) : (
                      <>{currentReplayTick + 1} / {replayTickCount}</>
                    )}
                  </>
                ) : (
                  currentTick
                )}
              </span>
              {isGameOver && <span className="game-over-indicator">🏁 Game Over</span>}
            </div>
            {showReplayMode && replayingGameName && (
              <div className="replay-game-name">
                📼 Replaying: <strong>{replayingGameName}</strong>
              </div>
            )}
          </div>
          
          {/* Playback Controls - Only show when game is initialized */}
          {gameInitialized && (
            <div className="playback-controls-container">
              <PlaybackControls
                currentFrame={showReplayMode ? currentReplayTick : currentDisplayIndex}
                totalFrames={showReplayMode ? replayTickCount : allGameStates.length}
                isPlaying={isPlaying}
                onPlayPause={handlePlayPause}
                onRewind={handleRewind}
                onForward={handleForward}
                onSetFrame={handleSetFrame}
                onSpeedChange={handleSpeedChange}
                onRestart={handleRestart}
                onExitReplay={showReplayMode ? handleExitReplay : null}
                isFetchingTick={isFetchingTick}
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
