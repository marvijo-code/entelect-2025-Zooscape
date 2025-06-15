import React, { useState, useEffect, useCallback, useRef, useMemo } from 'react';
import { HubConnectionBuilder, LogLevel, HubConnectionState } from "@microsoft/signalr";
import Grid from './components/Grid.jsx';
import Leaderboard from './components/Leaderboard.jsx';
import GameSelector from './components/GameSelector.jsx';
import PlaybackControls from './components/PlaybackControls.jsx';
import TabsContainer from './components/TabsContainer.jsx';
import ConnectionDebugger from './components/ConnectionDebugger.jsx';
import TestRunner from './components/TestRunner.jsx';
import './App.css';
import './styles/ConnectionDebugger.css';
import JsonPasteLoader from './components/JsonPasteLoader.jsx';

const HUB_URL = import.meta.env.VITE_HUB_URL || "http://localhost:5000/bothub";
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5008/api'; // API server running on port 5008

const App = () => {
  const [connection, setConnection] = useState(null);
  const [allGameStates, setAllGameStates] = useState([]);
  const [currentDisplayIndex, setCurrentDisplayIndex] = useState(0);
  const livePlaybackTimerRef = useRef(null);
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
  const [activeTabIndex, setActiveTabIndex] = useState(1);
  const [liveTickQueue, setLiveTickQueue] = useState([]);
  const [currentGameState, setCurrentGameState] = useState(null);
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
  const animalColors = useMemo(() => ['blue', 'green', 'purple', 'cyan', 'magenta', 'yellow', 'lime', 'teal', 'red', 'orange', 'pink', 'brown', 'gray', 'olive'], []);

  const namedColorsToHex = {
    blue: '#0000FF', green: '#008000', purple: '#800080', cyan: '#00FFFF',
    magenta: '#FF00FF', yellow: '#FFFF00', lime: '#00FF00', teal: '#008080',
    red: '#FF0000', orange: '#FFA500', pink: '#FFC0CB', brown: '#A52A2A',
    gray: '#808080', olive: '#808000',
    transparent: '#00000000' // Special case for transparent
  };

  const getTextColorForBackground = (bgColorName) => {
    if (!bgColorName || bgColorName === 'transparent') {
      return 'var(--text-primary)'; // Default text color from CSS variables for transparent backgrounds
    }

    const lowerBgColorName = bgColorName.toLowerCase();
    let hexColor = namedColorsToHex[lowerBgColorName];

    if (!hexColor) {
      if (lowerBgColorName.startsWith('#')) {
        hexColor = lowerBgColorName;
      } else {
        // If the color name is not in our map and not a hex, default or log warning
        // For simplicity, defaulting. A more robust solution might try to compute style from a dummy element.
        console.warn(`[getTextColorForBackground] Unknown color name: ${bgColorName}, defaulting text color.`);
        return 'var(--text-primary)';
      }
    }

    if (hexColor === '#00000000') return 'var(--text-primary)'; // Explicitly handle transparent hex

    // Ensure hexColor is valid for parsing (e.g., #RRGGBB)
    if (!/^#[0-9A-F]{6}$/i.test(hexColor) && !/^#[0-9A-F]{8}$/i.test(hexColor)) {
      console.warn(`[getTextColorForBackground] Invalid hex color format: ${hexColor} from ${bgColorName}, defaulting text color.`);
      return 'var(--text-primary)';
    }

    const r = parseInt(hexColor.slice(1, 3), 16);
    const g = parseInt(hexColor.slice(3, 5), 16);
    const b = parseInt(hexColor.slice(5, 7), 16);

    // Calculate luminance (standard formula)
    const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;

    return luminance > 0.5 ? 'black' : 'white'; // Threshold 0.5 is common
  };

  const handleLoadPastedJson = useCallback((jsonData) => {
    console.log("Loading pasted JSON data:", jsonData);
    setError(null);
    setIsReplaying(false);
    setReplayGameId(null);
    setReplayingGameName("Pasted JSON Data");
    setGameInitialized(true);
    setIsPlaying(false);
    setShowReplayMode(true); // Keep replay controls for consistency

    const processedJsonData = {
      tick: 0, // Default tick
      ...jsonData,
      tick: jsonData.tick !== undefined ? jsonData.tick : (jsonData.Tick !== undefined ? jsonData.Tick : 0),
    };

    setAllGameStates([processedJsonData]);
    setCurrentDisplayIndex(0);

    if (processedJsonData.animals || processedJsonData.Animals) {
      const animalsList = processedJsonData.animals || processedJsonData.Animals;
      setAnimalColorMap(prevColors => {
        const newColors = { ...prevColors };
        animalsList.forEach(animal => {
          const animalId = animal.id || animal.Id;
          if (animalId && !newColors[animalId]) {
            newColors[animalId] = animalColors[Math.floor(Math.random() * animalColors.length)];
          }
        });
        return newColors;
      });
    }
  }, [animalColors]);

  const playbackTimerRef = useRef(null);
  const [processingQueue, setProcessingQueue] = useState([]);
  const processingTimeoutRef = useRef(null);
  const [selectedFile, setSelectedFile] = useState(null);
  const [finalScoresMap, setFinalScoresMap] = useState({});
  const [shouldShowCreateModal, setShouldShowCreateModal] = useState(false);

  const fetchAndDisplayReplayTick = useCallback(async (gameId, tickNumber) => {
    if (isFetchingTick) return;
    setIsFetchingTick(true);
    setError(null);
    console.log(`Fetching replay tick: ${gameId}, tick number: ${tickNumber}`);

    const controller = new AbortController();
    const timeoutId = setTimeout(() => {
      controller.abort();
      console.warn(`Fetch for tick ${tickNumber} timed out after 15 seconds.`);
    }, 15000); // 15 seconds timeout

    try {
      // API ticks are 1-based, internal (currentReplayTick) is 0-based
      const apiTickNumber = tickNumber + 1;
      const url = `${API_BASE_URL}/replay/${gameId}/${apiTickNumber}`;
      console.log(`Making API request to: ${url}`);

      const response = await fetch(url, { signal: controller.signal });
      clearTimeout(timeoutId); // Clear timeout if fetch completes in time

      console.log(`API response status: ${response.status}`);

      if (!response.ok) {
        if (response.statusText === 'Aborted') { // Or check controller.signal.aborted
          throw new Error(`Fetch aborted for tick ${apiTickNumber}: Request timed out.`);
        }
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
      clearTimeout(timeoutId); // Ensure timeout is cleared on any error
      console.error("Error fetching replay tick:", e);
      setError(`Failed to load tick ${tickNumber + 1}: ${e.message}`);
      setAllGameStates([]); // Clear states on error
    }
    setIsFetchingTick(false);
  }, [isFetchingTick, animalColors, animalColorMap, API_BASE_URL]);

  const handleReplayGame = useCallback(async (gameData) => {
    console.log(`Starting replay for game:`, gameData);

    // Handle both game object and gameId string
    const gameId = typeof gameData === 'string' ? gameData : gameData.id;
    const gameName = typeof gameData === 'string' ? gameData : (gameData.name || `Game ${gameData.id}`);
    const tickCount = typeof gameData === 'object' && gameData.tickCount ? gameData.tickCount : 0;

    console.log("handleReplayGame - Received gameData:", gameData); // DEBUG LINE
    console.log("handleReplayGame - Original gameId:", gameId);
    console.log("handleReplayGame - Extracted tickCount:", tickCount); // DEBUG LINE

    if (!gameId) {
      setError('Invalid game selected - no ID found');
      return;
    }

    // Construct full path for selectedFile for display purposes.
    // This assumes the API server (localhost:5008) is serving logs from the specified fixed base path.
    // This is for UI display only. The relative gameId is used for API calls.
    const assumedApiLogsBasePath = 'C:\\dev\\2025-Zooscape\\visualizer-2d\\logs\\';

    // Check if gameId already looks like an absolute path (e.g., from a different source or future API change)
    const isAbsolute = gameId.includes(':') || gameId.startsWith('/') || gameId.startsWith('\\');

    const fullDisplayPath = isAbsolute
      ? gameId
      : assumedApiLogsBasePath + gameId.replace(/\//g, '\\');

    console.log("handleReplayGame - Original gameId (for API calls):", gameId);
    console.log("handleReplayGame - Assumed API logs base path (for display):", assumedApiLogsBasePath);
    console.log("handleReplayGame - Constructed fullDisplayPath (for UI):", fullDisplayPath);

    try {
      setSelectedFile(fullDisplayPath); // Store the constructed full path for UI display
      setReplayingGameName(gameName);
      setReplayGameId(gameId); // Use the ORIGINAL relative gameId for API calls
      setShowReplayMode(true);
      setIsReplaying(true);
      setIsPlaying(false); // Start paused
      setGameInitialized(true); // Game is considered initialized once a replay is selected
      setCurrentReplayTick(0);
      setError(null);

      // Use tick count from game object instead of making API call
      setReplayTickCount(tickCount);

      // Load initial tick
      await fetchAndDisplayReplayTick(gameId, 0);

      // Fetch the last tick to get final scores if tickCount is available
      if (tickCount > 0) {
        try {
          const finalTickResponse = await fetch(`${API_BASE_URL}/replay/${gameId}/${tickCount}`);
          if (finalTickResponse.ok) {
            const finalTickData = await finalTickResponse.json();
            const animalsInFinalTick = finalTickData.animals || finalTickData.Animals || [];
            const finalScores = {};
            animalsInFinalTick.forEach(animal => {
              const id = animal.id || animal.Id;
              const score = animal.score || animal.Score || 0;
              if (id) {
                finalScores[id] = score;
              }
            });
            setFinalScoresMap(finalScores);
            console.log("Final scores map populated:", finalScores);
          } else {
            console.warn(`Failed to fetch final tick data to populate final scores: ${finalTickResponse.status}`);
          }
        } catch (e) {
          console.error("Error fetching final tick data for scores:", e);
        }
      }

      console.log(`Replay initialized for ${gameId}, total ticks: ${tickCount}`);
    } catch (error) {
      console.error('Error starting replay:', error);
      setError(`Failed to start replay: ${error.message}`);
      // Reset replay state on error
      setShowReplayMode(false);
      setIsReplaying(false);
      setSelectedFile(null);
      setReplayingGameName('');
      setGameInitialized(false);
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
  const isProcessingLiveTickRef = useRef(false); // To prevent concurrent processing of live ticks
  const lastTickTimeRef = useRef(Date.now());

  // Memoize grid data to prevent unnecessary Grid re-renders
  const gridData = useMemo(() => {
    if (!currentGameState) {
      console.log("No currentGameState available for grid");
      return { cells: [], animals: [], zookeepers: [], leaderBoard: {} };
    }

    console.log("Current game state keys:", Object.keys(currentGameState));
    console.log("Current game state structure:", currentGameState);

    const cells = currentGameState.cells || currentGameState.Cells || [];
    const animals = currentGameState.animals || currentGameState.Animals || [];
    const zookeepers = currentGameState.zookeepers || currentGameState.Zookeepers || [];
    // Ensure LeaderBoard is an object, even if missing or null in currentGameState
    const leaderBoard = currentGameState.LeaderBoard || currentGameState.leaderBoard || {};

    console.log(`Grid data extracted - Cells: ${cells.length}, Animals: ${animals.length}, Zookeepers: ${zookeepers.length}`);
    console.log(`LeaderBoard data for Grid:`, leaderBoard);

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
      zookeepers,
      leaderBoard // Pass the per-tick LeaderBoard data
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
        if (prevQueue.length > 100) { // Increased queue limit for robustness
          // Simple truncation if queue gets too long, consider more sophisticated dropping
          console.warn(`Live tick queue > 100 (${prevQueue.length}), dropping oldest ${prevQueue.length - 90} ticks.`);
          tickMetricsRef.current.droppedCount += (prevQueue.length - 90);
          return [...prevQueue.slice(-90), newGameState];
        }
        return [...prevQueue, newGameState];
      });

      // The useEffect watching liveTickQueue will handle processing initiation.

    } catch (e) {
      setError(`Failed to process game update: ${e.message}`);
    }
  }, []);

  // Effect 1: Create and store connection object. Stop it on component unmount.
  useEffect(() => {
    let connectionInstance = null;

    console.log("Connection to hub URL:", HUB_URL);
    console.log("Registered handlers: gametick, gamestate, tick, gameover");

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

  // Effect to manage live tick processing using setTimeout and playbackSpeed
  useEffect(() => {
    const processLiveTickInternal = () => {
      if (showReplayMode || liveTickQueue.length === 0 || isProcessingLiveTickRef.current) {
        return;
      }

      isProcessingLiveTickRef.current = true;
      const processStart = performance.now();

      const tickToProcess = liveTickQueue[0];

      setCurrentGameState(prevState => {
        if (prevState && tickToProcess.tick < prevState.tick && tickToProcess.gameId === prevState.gameId) {
          console.warn(`[LiveTick] Received older tick (${tickToProcess.tick}) than current (${prevState.tick}). Ignoring.`);
          isProcessingLiveTickRef.current = false; // Release lock if ignoring
          // Still remove from queue if it's an old tick we are skipping
          setLiveTickQueue(prevQ => prevQ.slice(1)); 
          return prevState;
        }
        tickMetricsRef.current.processedCount++;
        return tickToProcess;
      });

      const animalsInTick = tickToProcess.animals || tickToProcess.Animals || [];
      if (animalsInTick.length > 0) {
        const newColors = {};
        animalsInTick.forEach(animal => {
          const animalKey = animal.id || animal.Id;
          if (!animalKey) {
            console.warn('Animal in live tick data is missing an ID', animal);
            return;
          }
          if (!animalColorMap[animalKey]) {
            newColors[animalKey] = animalColors[Math.floor(Math.random() * animalColors.length)];
          }
        });
        if (Object.keys(newColors).length > 0) {
          setAnimalColorMap(prevColors => ({ ...prevColors, ...newColors }));
        }
      }
      
      // Remove the processed tick from the queue
      // This will trigger the useEffect again if more ticks are present.
      setLiveTickQueue(prevQ => prevQ.slice(1));

      const processEnd = performance.now();
      const processTime = processEnd - processStart;
      tickMetricsRef.current.lastProcessTime = processTime;
      tickMetricsRef.current.averageProcessTime =
        (tickMetricsRef.current.averageProcessTime * 0.9) + (processTime * 0.1);

      lastTickTimeRef.current = Date.now();
      isProcessingLiveTickRef.current = false;
    };

    // Clear any existing timer
    if (playbackTimerRef.current) {
      clearTimeout(playbackTimerRef.current);
    }

    if (!showReplayMode && liveTickQueue.length > 0 && !isProcessingLiveTickRef.current) {
      const timerInterval = 1000 / playbackSpeed; 
      playbackTimerRef.current = setTimeout(processLiveTickInternal, timerInterval);
    }

    return () => {
      if (playbackTimerRef.current) {
        clearTimeout(playbackTimerRef.current);
        playbackTimerRef.current = null;
      }
    };
  }, [liveTickQueue, showReplayMode, playbackSpeed, animalColorMap, animalColors, setCurrentGameState, setAnimalColorMap, setLiveTickQueue]);

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
      const timerInterval = 1000 / playbackSpeed; // 1000ms / speed = interval in ms (1s at 1x)

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
    console.log('Exiting replay mode');
    setShowReplayMode(false);
    setIsReplaying(false);
    setIsPlaying(false);
    setCurrentReplayTick(0);
    setReplayTickCount(0);
    setReplayGameId(null);
    setReplayingGameName('');
    setSelectedFile(null); // Clear selected file
    setAllGameStates([]);
    setError(null);

    // Reset the active tab back to Live Feed when exiting replay
    setActiveTabIndex(0);
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
  }, [connection]); // Added missing dependency: connection

  const handleTestGameStateSelected = useCallback((gameState, displayName) => {
    console.log("Test game state selected:", displayName);

    // Enter replay mode if not already
    if (!showReplayMode) {
      setShowReplayMode(true);
      setIsReplaying(true);
    }

    // Set the game state
    setAllGameStates([gameState]);
    setCurrentDisplayIndex(0);
    setGameInitialized(true);
    setIsPlaying(false);
    setReplayingGameName(displayName);

    // Update animal colors
    if (gameState.animals) {
      const newColors = {};
      gameState.animals.forEach((animal, index) => {
        const animalKey = animal.id || animal.Id;
        if (animalKey) {
          newColors[animalKey] = animalColors[index % animalColors.length];
        }
      });
      setAnimalColorMap(newColors);
    }

    setError(null);
  }, [showReplayMode, animalColors]);

  // Effect for keyboard navigation in replay mode
  useEffect(() => {
    const handleKeyDown = (event) => {
      if (!showReplayMode || !replayGameId) return;

      // Prevent interference with input fields or other interactive elements
      if (event.target.tagName === 'INPUT' || event.target.tagName === 'SELECT' || event.target.tagName === 'BUTTON' || event.target.isContentEditable) {
        return;
      }

      if (event.key === 'ArrowLeft') {
        event.preventDefault();
        handleRewind();
      } else if (event.key === 'ArrowRight') {
        event.preventDefault();
        handleForward();
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [showReplayMode, replayGameId, handleRewind, handleForward]);

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


  // Memoize current tick display
  const currentTick = useMemo(() => {
    if (!currentGameState) return 0;
    return currentGameState.tick !== undefined ? currentGameState.tick : currentGameState.Tick || 0;
  }, [currentGameState]);

  // Memoize per-tick scoreboard for replay mode
  const perTickScoreboard = useMemo(() => {
    // Condition to show scores: Must have currentGameState AND (isReplaying OR (live mode AND connected AND game initialized))
    console.log('[Scoreboard] Recomputing. animalColorMap:', JSON.stringify(animalColorMap));
    const showScores = currentGameState &&
      (isReplaying || (!showReplayMode && isConnected && gameInitialized));

    if (!showScores) {
      return null;
    }

    const animals = currentGameState.animals || currentGameState.Animals || [];
    const leaderBoard = currentGameState.LeaderBoard || currentGameState.leaderBoard || {};

    let scoresData = [];

    // Try to get scores from leaderBoard first
    if (leaderBoard && Object.keys(leaderBoard).length > 0) {
      // Create a mapping of animal IDs to nicknames for quick lookup
      const animalIdToNickname = animals.reduce((acc, animal) => {
        const id = animal.id || animal.Id;
        const nickname = animal.nickname || animal.Nickname || `Bot-${id}`;
        if (id) {
          acc[id] = nickname;
        }
        return acc;
      }, {});

      scoresData = Object.entries(leaderBoard)
        .map(([id, score]) => ({
          id,
          nickname: animalIdToNickname[id] || `Bot-${id.substring(0, 6)}...`,
          score
        }))
        .sort((a, b) => b.score - a.score);
    } else if (animals && animals.length > 0) {
      // Try to get scores directly from animals array
      scoresData = animals
        .map((animal) => {
          const id = animal.id || animal.Id;
          const nickname = animal.nickname || animal.Nickname || `Bot-${id}`;
          const score = animal.score || animal.Score || 0;
          return {
            id,
            nickname,
            score
          };
        })
        .filter(entry => entry.score !== undefined && entry.score !== null)
        .sort((a, b) => b.score - a.score);
    }

    if (scoresData.length === 0) {
      return (
        <div className="replay-scoreboard">
          <h4>Current Scores</h4>
          <p>No scores available</p>
        </div>
      );
    }

    return (
      <div className="replay-scoreboard">
        <h4>Current Scores</h4>
        <ul>
          {scoresData.map((entry, index) => {
            const botIdForColor = entry.id;
            const colorToApply = animalColorMap[botIdForColor] || 'transparent';
            console.log(`[Scoreboard] Rendering bot ${entry.nickname} (ID: ${botIdForColor}), applying bgColor: ${colorToApply}`);
            return (
              <li
                key={botIdForColor || index}
                style={{ backgroundColor: colorToApply, color: getTextColorForBackground(colorToApply) }}
              >
                <span>{index + 1}. {entry.nickname}:</span>
                <span>{entry.score} / {finalScoresMap[entry.id] !== undefined ? finalScoresMap[entry.id] : 'N/A'}</span>
              </li>
            );
          })}
        </ul>
      </div>
    );
  }, [isReplaying, currentGameState, finalScoresMap, showReplayMode, isConnected, gameInitialized, animalColorMap]);

  // Render active tab content
  const renderActiveTabContent = useCallback(() => {
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
          <JsonPasteLoader onLoadJson={handleLoadPastedJson} onError={setError} />
        );
      case 2: // Was Game Selector, now Connection if not showReplayMode, or Game Selector if showReplayMode
        if (showReplayMode) {
          return (
            <GameSelector
              onGameSelected={handleReplayGame}
              apiBaseUrl={API_BASE_URL}
              setError={setError}
            />
          );
        } else { // Not showReplayMode, so this is Connection tab
          return (
            <ConnectionDebugger
              connection={connection}
              isConnected={isConnected}
              error={error}
            />
          );
        }
      case 3: // Was Test Runner, now only if showReplayMode
        if (showReplayMode) {
          return (
            <TestRunner
              onGameStateSelected={handleTestGameStateSelected}
              apiBaseUrl="http://localhost:5009/api"
              currentGameState={currentGameState}
              currentGameStateName={selectedFile ?
                (typeof selectedFile === 'string' && (selectedFile.includes('/') || selectedFile.includes('\\'))
                  ? selectedFile.split(/[\/\\]/).pop()
                  : selectedFile) :
                (replayingGameName || 'current-state.json')}
              shouldShowCreateModal={shouldShowCreateModal}
              onCreateModalChange={setShouldShowCreateModal}
            />
          );
        }
        return null; // Or some placeholder if Test Runner is hidden but tab 3 is active somehow
      default:
        return null;
    }
  }, [activeTabIndex, leaderboardData, leaderboardLoading, leaderboardStatusMessage, fetchAggregateLeaderboardData, handleReplayGame, handleLoadPastedJson, setError, connection, isConnected, showReplayMode, API_BASE_URL, handleTestGameStateSelected, currentGameState, selectedFile, replayingGameName, shouldShowCreateModal]);

  // Effect to update activeTabIndex if it becomes invalid when showReplayMode changes
  useEffect(() => {
    if (!showReplayMode && activeTabIndex === 3) { // Test Runner tab was active, but now hidden
      setActiveTabIndex(2); // Switch to Connection tab (index 2 is Connection when not in replay mode)
    }
    // If in replay mode, and the "Connection" tab (index 2) was active but now should be GameSelector because 'connection' is null (or not live)
    // This case might be redundant if TabsContainer already handles disabling/hiding Connection tab when not applicable.
    // However, if activeTabIndex could be 2 while showReplayMode is true AND connection is null, this ensures it switches.
    // For now, let's assume TabsContainer correctly manages tab visibility/availability.
    // If connection is lost while on Connection tab (index 2) and NOT in replay mode, user stays on Connection tab to see status.
  }, [showReplayMode, activeTabIndex, connection]);

  // ...

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
              leaderBoard={gridData.leaderBoard}
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
                  <>{currentReplayTick + 1} / {replayTickCount}</>
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

          {perTickScoreboard}
          {/* Playback Controls - visible in replay mode or when game is initialized from pasted JSON */}
          {(gameInitialized && currentGameState && (isReplaying || (!showReplayMode && isConnected))) && (
            <div className="playback-controls-container">
              <PlaybackControls
                currentFrame={isReplaying ? currentReplayTick : 0} // For pasted JSON, currentFrame is 0
                totalFrames={isReplaying ? replayTickCount : 1}    // For pasted JSON, totalFrames is 1
                isPlaying={isPlaying} // Will be false for pasted JSON initially
                onPlayPause={handlePlayPause} // Less relevant for single frame
                onRewind={() => isReplaying ? handleRewind() : console.log("Rewind for pasted JSON (no-op)")}
                onForward={() => isReplaying ? handleForward() : console.log("Forward for pasted JSON (no-op)")}
                onSetFrame={(frame) => isReplaying ? handleSetFrame(frame) : console.log("SetFrame for pasted JSON to", frame, "(no-op)")}
                onSpeedChange={handleSpeedChange} // Less relevant for single frame
                onRestart={() => isReplaying ? handleRestart() : console.log("Restart for pasted JSON (no-op)")}
                onExitReplay={showReplayMode ? handleExitReplay : null} // Relevant for replay mode
                isFetchingTick={isFetchingTick}
                isSingleFrameView={!isReplaying && gameInitialized && !connection && allGameStates.length === 1} // Indicate single frame mode
              />
            </div>
          )}

          {showReplayMode && selectedFile && (
            <div className="current-file-display">
              <h4>Current Replay File</h4>
              <div className="file-info">
                <span className="filename">
                  {typeof selectedFile === 'string' && (selectedFile.includes('/') || selectedFile.includes('\\'))
                    ? selectedFile.split(/[/\\]/).pop()
                    : selectedFile}
                </span>
                <button
                  className="copy-path-button"
                  onClick={() => {
                    navigator.clipboard.writeText(selectedFile).then(() => {
                      // Optional: Show a brief success indicator
                      const button = document.querySelector('.copy-path-button');
                      const originalText = button.textContent;
                      button.textContent = '✓';
                      setTimeout(() => {
                        button.textContent = originalText;
                      }, 1000);
                    }).catch(err => {
                      console.error('Failed to copy path:', err);
                    });
                  }}
                  title={`Copy full path: ${selectedFile}`}
                >
                  📋
                </button>
              </div>
              <div className="full-path" title={selectedFile}>
                {selectedFile}
              </div>
              <div className="file-actions">
                <button
                  className="create-test-button"
                  onClick={() => {
                    setActiveTabIndex(3); // Test Runner is tab index 3 in replay mode
                    setShouldShowCreateModal(true);
                  }}
                  disabled={!currentGameState}
                  title={!currentGameState ? "No game state available" : "Create a test with current game state"}
                >
                  ➕ Create Test from Current State
                </button>
              </div>
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

          {/* Tab Content Area */}
          <div className="tab-content">
            {renderActiveTabContent()}
          </div>
        </div>
      </div>
    </div>
  );
  // ...

};

export default App;
