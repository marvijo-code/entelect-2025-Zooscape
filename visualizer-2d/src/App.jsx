import React, { useState, useEffect, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import Grid from './components/Grid.jsx';
import Leaderboard from './components/Leaderboard.jsx';
import './App.css';

const HUB_URL = "http://localhost:5000/bothub";

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

  const animalColors = ['blue', 'green', 'orange', 'purple', 'cyan', 'magenta', 'yellow', 'lime'];

  const API_BASE_URL = 'http://localhost:5008/api'; // API server running on port 5008

  const fetchAggregateLeaderboardData = useCallback(async () => {
    setLeaderboardLoading(true);
    setLeaderboardStatusMessage('Fetching tournament file list...');
    setLeaderboardData([]); // Clear previous data
    let tournamentLogFilePaths = []; // These will be like ['run_id1/file1.json', 'run_id2/file2.json']
    try {
      const tournamentFilesResponse = await fetch(`${API_BASE_URL}/tournament_files`);
      if (!tournamentFilesResponse.ok) {
        const errorData = await tournamentFilesResponse.text();
        console.error(`API Error! Status: ${tournamentFilesResponse.status} - Failed to fetch tournament files. Server says: ${errorData}`);
        setLeaderboardData([]);
        setLeaderboardStatusMessage('Failed to load tournament file list. API error.');
        setLeaderboardLoading(false);
        return;
      }
      const tournamentFilesData = await tournamentFilesResponse.json();
      if (!tournamentFilesData || !Array.isArray(tournamentFilesData.tournament_files)) {
        console.error('Invalid format from /api/tournament_files: Expected an object with a tournament_files array.');
        setLeaderboardData([]);
        setLeaderboardStatusMessage('Failed to load tournament file list. Invalid data format from API.');
        setLeaderboardLoading(false);
        return;
      }
      tournamentLogFilePaths = tournamentFilesData.tournament_files;
    } catch (error) {
      console.error('Network error or failed to parse response from /api/tournament_files:', error);
      setLeaderboardData([]);
      setLeaderboardStatusMessage('Failed to load tournament file list. Network error or bad response.');
      setLeaderboardLoading(false);
      return;
    }

    if (tournamentLogFilePaths.length === 0) {
      console.warn('/api/tournament_files returned no files. No aggregate data to load.');
      setLeaderboardData([]);
      setLeaderboardStatusMessage('No tournament log files found to process.');
      setLeaderboardLoading(false);
      return;
    }
    const botStats = {}; // Stores { 'BotNickname': { wins: 0, totalGamesParticipated: 0, lastScore: 0, id: null } }
    let overallTotalGamesProcessed = 0;
    setLeaderboardStatusMessage(`Preparing to process ${tournamentLogFilePaths.length} log file(s)...`);

    // Using for...of loop; for indexed progress, consider for...i or .map/.forEach with index
    for (const [index, relativeLogPath] of tournamentLogFilePaths.entries()) {
      setLeaderboardStatusMessage(`Processing log ${index + 1} of ${tournamentLogFilePaths.length}: ${relativeLogPath}`);
      try {
        // relativeLogPath is like 'run_id/log_filename.json'
        const response = await fetch(`${API_BASE_URL}/logs/${relativeLogPath}`);
        if (!response.ok) {
          const errorData = await response.text();
          console.error(`API Error! Status: ${response.status} - Failed to fetch log /api/logs/${relativeLogPath}. Server says: ${errorData}`);
          continue; // Skip this file
        }
        const gameData = await response.json();
        let animalsInGame = [];

        // Extract animals array (handles various possible structures)
        if (gameData && Array.isArray(gameData.Animals)) {
          animalsInGame = gameData.Animals;
        } else if (gameData && Array.isArray(gameData.animals)) {
          animalsInGame = gameData.animals;
        } else if (gameData.WorldStates && gameData.WorldStates.length > 0) {
          const lastState = gameData.WorldStates[gameData.WorldStates.length - 1];
          animalsInGame = lastState.Animals || lastState.animals || [];
        } else if (gameData.worldStates && gameData.worldStates.length > 0) {
          const lastState = gameData.worldStates[gameData.worldStates.length - 1];
          animalsInGame = lastState.Animals || lastState.animals || [];
        }

        if (!animalsInGame || animalsInGame.length === 0) {
          console.warn(`No animal data found or array is empty in log from /api/logs/${relativeLogPath}`);
          continue; // Skip if no animals
        }

        overallTotalGamesProcessed++;
        let gameWinnerNickname = null;
        let highestScore = -Infinity;

        for (const animal of animalsInGame) {
          const nickname = animal.Nickname || animal.NickName;
          if (!nickname) continue;

          if (!botStats[nickname]) {
            botStats[nickname] = { wins: 0, totalGamesParticipated: 0, lastScore: 0, id: animal.Id || nickname };
          }
          botStats[nickname].totalGamesParticipated++;
          botStats[nickname].lastScore = animal.Score;
          if (animal.Id) botStats[nickname].id = animal.Id; // Prefer actual ID

          if (animal.Score > highestScore) {
            highestScore = animal.Score;
            gameWinnerNickname = nickname;
          } else if (animal.Score === highestScore) {
            gameWinnerNickname = null; // Undecided winner in case of a tie for simplicity
          }
        }

        if (gameWinnerNickname && botStats[gameWinnerNickname]) {
          botStats[gameWinnerNickname].wins++;
        }
      } catch (error) {
        console.error(`Network error or failed to parse log data from /api/logs/${relativeLogPath}:`, error);
      }
    }

    const finalLeaderboard = Object.entries(botStats).map(([nickname, stats]) => ({
      Id: stats.id,
      Nickname: nickname,
      Score: stats.lastScore, // Score from the last game they were in from the list
      Wins: stats.wins,
      TotalGamesParticipated: stats.totalGamesParticipated, // Bot's own participation count
      OverallTotalGames: overallTotalGamesProcessed, // Total log files processed
    }));

    setLeaderboardData(finalLeaderboard);
    setLeaderboardLoading(false);
    setLeaderboardStatusMessage(finalLeaderboard.length > 0 ? 'Leaderboard loaded.' : 'No data for leaderboard after processing logs.');
  }, []); // Dependencies are handled internally by fetching the manifest

  useEffect(() => {
    fetchAggregateLeaderboardData();
  }, [fetchAggregateLeaderboardData]);

  // Effect 1: Create and store connection object. Stop it on component unmount.
  useEffect(() => {
    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect()
      .build();
    setConnection(newConnection);

    return () => { // Cleanup for component unmount
      console.log("App unmounting, stopping SignalR connection.");
      // Ensure newConnection is not null and state is appropriate before stopping.
      if (newConnection &&
        (newConnection.state === signalR.HubConnectionState.Connected ||
          newConnection.state === signalR.HubConnectionState.Connecting ||
          newConnection.state === signalR.HubConnectionState.Reconnecting)) {
        newConnection.stop().catch(err => console.error("Error stopping connection on unmount", err));
      } else if (newConnection) {
        console.log("App unmounting, connection already stopped or not started. State:", newConnection.state);
      }
    };
  }, []); // HUB_URL is a const, no need to list as dependency

  // Effect 2: Start the connection when it's created and in Disconnected state.
  // This effect also handles registering the visualiser once connected.
  useEffect(() => {
    if (connection && connection.state === signalR.HubConnectionState.Disconnected) {
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
  }, [connection, setError]); // HUB_URL is const, setError is stable from useState

  const initializeGameHandler = useCallback((data) => {
    console.log("StartGame received:", data);
    setError(null);
    try {
      const payload = typeof data === 'string' ? JSON.parse(data) : data;
      // If StartGame contains full history, use that
      const history = payload.WorldStates || payload.worldStates;
      if (Array.isArray(history)) {
        setAllGameStates(history);
        setCurrentDisplayIndex(0);
        setGameInitialized(true);
        setIsPlaying(true);
        setIsGameOver(false);
        // Map initial animals to colors
        setAnimalColorMap(history[0]?.animals.reduce((map, a, idx) => {
          const animalKey = a.id || a.Id;
          if (animalKey) {
            map[animalKey] = animalColors[idx % animalColors.length];
          }
          return map;
        }, {}));
        console.log(`Game initialized with history (${history.length} states).`);
        return;
      }

      // Expecting a flat payload: { tick: ..., cells: ..., animals: ..., zookeepers: ... }
      if (payload && Array.isArray(payload.cells) && Array.isArray(payload.animals) && Array.isArray(payload.zookeepers)) {
        // If StartGame sends the full history as an array of these flat states (unlikely, but to cover)
        // This handler might be redundant if StartGame is not sent.
        // For now, assume StartGame (if sent) provides a single state or an array to pick the latest from.

        // If payload is a single GameState object
        setAllGameStates([payload]); // Store as an array with one state
        setCurrentDisplayIndex(0); // Or payload.tick if you want to use server's tick as index
        setGameInitialized(true);
        setIsPlaying(true);
        setIsGameOver(false);
        // Map initial animals to colors
        setAnimalColorMap(payload.animals.reduce((map, a, idx) => {
          const animalKey = a.id || a.Id;
          if (animalKey) {
            map[animalKey] = animalColors[idx % animalColors.length];
          }
          return map;
        }, {}));
        console.log("Game initialized with StartGame data (flat structure).");
      } else {
        console.error("StartGame: Invalid payload structure. Expected {cells, animals, zookeepers}", payload);
        setError("Failed to initialize game: Invalid data from server for StartGame.");
      }
    } catch (e) {
      console.error("Error parsing StartGame data:", e, "Raw data:", data);
      setError(`Failed to initialize game: ${e.message}`);
    }
  }, []);

  const tickStateChangedHandler = useCallback((data) => {
    setError(null);
    try {
      const newGameState = typeof data === 'string' ? JSON.parse(data) : data;

      // Expecting a flat payload: { timeStamp: ..., tick: ..., cells: ..., animals: ..., zookeepers: ... }
      if (newGameState && typeof newGameState.tick === 'number' && Array.isArray(newGameState.cells) && Array.isArray(newGameState.animals) && Array.isArray(newGameState.zookeepers)) {
        if (!gameInitialized) {
          setGameInitialized(true); // Initialize on first valid GameState
          console.log("Game initialized with first GameState data.");
          setIsPlaying(true);
          setIsGameOver(false);
          // Map animals on first tick
          setAnimalColorMap(prevMap => {
            const newMap = { ...prevMap };
            let colorIndex = Object.keys(newMap).length;
            newGameState.animals.forEach(animal => {
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
          // Add new state and auto-advance only if playing and at latest frame
          if (prevStates.length > 0 && newGameState.tick <= prevStates[prevStates.length - 1].tick) {
            return prevStates;
          }
          const updatedStates = [...prevStates, newGameState];
          if (isPlaying) {
            setCurrentDisplayIndex(updatedStates.length - 1);
          }
          return updatedStates;
        });
      } else {
        console.error("GameState: Invalid payload structure. Expected {tick, cells, animals, zookeepers}", newGameState);
        setError("Received invalid tick data.");
      }
    } catch (e) {
      console.error("Error parsing GameState data:", e, "Raw data:", data);
      setError(`Failed to process tick: ${e.message}`);
    }
  }, [gameInitialized, isPlaying]);

  const gameOverHandler = useCallback((message) => {
    console.log("GameOver received:", message);
    setIsGameOver(true);
    setIsPlaying(false);
  }, [setIsGameOver, setIsPlaying]);

  const handleJoinGame = useCallback(() => {
    if (!connection) return;
    setError(null);
    connection.start()
      .then(() => {
        console.log('SignalR Connected!');
        setIsConnected(true);
        return connection.invoke('RegisterVisualiser');
      })
      .then(() => console.log('Visualiser registered'))
      .catch(e => {
        console.error('Connection/Registration failed on retry:', e);
        setIsConnected(false);
        setError(`SignalR Connection/Registration Failed: ${e.message}. Check server.`);
      });
  }, [connection]);

  // Effect 3: Attach SignalR event listeners when connection is available
  useEffect(() => {
    if (!connection) return;
    // Attach event handlers
    connection.on('StartGame', initializeGameHandler);
    connection.on('GameState', tickStateChangedHandler);
    connection.on('GameOver', gameOverHandler);
    // Connection state handlers for UI updates
    connection.onreconnecting(error => { console.warn('SignalR reconnecting...', error); setIsConnected(false); });
    connection.onreconnected(id => { console.log('SignalR reconnected', id); setIsConnected(true); });
    connection.onclose(error => { console.log('SignalR closed', error); setIsConnected(false); });

    return () => {
      connection.off('StartGame', initializeGameHandler);
      connection.off('GameState', tickStateChangedHandler);
      connection.off('GameOver', gameOverHandler);
      connection.off('reconnecting');
      connection.off('reconnected');
      connection.off('close');
    };
  }, [connection, initializeGameHandler, tickStateChangedHandler, gameOverHandler]);

  const handleReplay = () => {
    if (allGameStates.length === 0) {
      setError("No game data to replay.");
      return;
    }
    setIsGameOver(false);
    setCurrentDisplayIndex(0);
    setIsPlaying(true);
  };

  const handleRewind = () => { setIsPlaying(false); setCurrentDisplayIndex(prev => Math.max(0, prev - 1)); };
  const handlePlayPause = () => {
    if (!isPlaying && allGameStates.length > 0) {
      // If currently at the last frame, restart from beginning
      if (currentDisplayIndex >= allGameStates.length - 1) {
        setCurrentDisplayIndex(0);
      }
      // Otherwise, just resume from current frame
    }
    setIsPlaying(prev => !prev);
  };

  const handleForward = () => { setIsPlaying(false); setCurrentDisplayIndex(prev => Math.min(allGameStates.length - 1, prev + 1)); };

  const currentGameState = allGameStates[currentDisplayIndex] || null;
  const scoreboardData = currentGameState?.animals ? [...currentGameState.animals].sort((a, b) => b.score - a.score) : [];

  useEffect(() => {
    if (!isPlaying || isGameOver || allGameStates.length === 0) return;
    const interval = setInterval(() => {
      setCurrentDisplayIndex(prev => prev < allGameStates.length - 1 ? prev + 1 : prev);
    }, 500);
    return () => clearInterval(interval);
  }, [isPlaying, allGameStates.length, isGameOver]);

  // Auto-advance to the latest frame if new states are appended and isPlaying is true
  useEffect(() => {
    if (!isPlaying || isGameOver) return;
    if (currentDisplayIndex >= allGameStates.length - 2 && allGameStates.length > 0) {
      setCurrentDisplayIndex(allGameStates.length - 1);
    }
  }, [allGameStates.length, isPlaying, isGameOver]);

  // Debug: log render state
  // useEffect(() => {
  //   // console.log('App Render Debug:', {
  //   //   isConnected,
  //   //   loadedStates: allGameStates.length,
  //   //   currentDisplayIndex,
  //   //   currentTick: currentGameState?.tick,
  //   //   hasGameState: currentGameState != null,
  //   // });

  // }, [isConnected, allGameStates, currentDisplayIndex, currentGameState]);

  return (
    <div className="App">
      <div className="main-content">
        <div className="grid-container">
          {currentGameState ? (
            <Grid gameState={currentGameState} colorMap={animalColorMap} />
          ) : (
            // Show message if not gameInitialized AND not replaying from log
            !isReplaying && (
              <p>
                {isConnected
                  ? `Waiting for game data... (${allGameStates.length} loaded, idx ${currentDisplayIndex})`
                  : 'Connecting...'}
              </p>
            )
          )}
        </div>
        <div className="App">
          <header className="App-header">
            <h1>Zooscape 2D Visualizer</h1>

          </header>
          {!isConnected && (
            <>
              <div className="spinner" />
              <button onClick={handleJoinGame}>Join New Game</button>
            </>
          )}
          {isConnected && error && <div className="error-banner">{error}</div>}
          {gameInitialized && (
            <div className="scoreboard-container">
              <h2>Scoreboard</h2>
              {scoreboardData.length > 0 ? (
                <table>
                  <thead>
                    <tr>
                      <th>NickName</th>
                      <th>Score</th>
                      <th>Captured</th>
                      <th>Distance</th>
                      <th>Viable</th>
                    </tr>
                  </thead>
                  <tbody>
                    {scoreboardData.map((animal) => (
                      <tr key={animal.id} style={{ backgroundColor: animalColorMap[animal.id] }}>
                        <td>{animal.nickname}</td>
                        <td>{animal.score}</td>
                        <td>{animal.capturedCounter}</td>
                        <td>{animal.distanceCovered}</td>
                        <td>{animal.isViable ? 'Yes' : 'No'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              ) : (
                <p>No animal data.</p>
              )}
            </div>
          )}
          <div className="controls">
            <div>
              <button onClick={handleReplay} disabled={allGameStates.length === 0 || isPlaying || !isGameOver}>
                Replay Last Game
              </button>
              <button onClick={() => { setIsReplaying(false); setIsPlaying(true); }} disabled={!isReplaying && isPlaying}>
                Resume Live
              </button>
              <button onClick={handleRewind} disabled={!gameInitialized || currentDisplayIndex === 0 || isPlaying}>
                Rewind
              </button>
              <button onClick={handlePlayPause} disabled={!gameInitialized || isGameOver}>
                {isPlaying ? 'Pause' : 'Play'}
              </button>
              <button onClick={handleForward} disabled={!gameInitialized || currentDisplayIndex >= allGameStates.length - 1 || isPlaying}>
                Forward
              </button>
            </div>
            <div>
              <span>
                Tick: {currentGameState?.tick ?? 'N/A'} (Frame {currentDisplayIndex + 1}/{allGameStates.length})
              </span>
              <span style={{ marginLeft: '10px', fontSize: '0.8em', color: '#555' }}>
                States: {allGameStates.length}, Index: {currentDisplayIndex}
              </span>
              {isGameOver && <span className="game-over-text">GAME OVER</span>}
            </div>
          </div>
          <div className="legend">
            <h3 className="legend-header">Legend</h3>
            <ul className="legend-list">
              <li>
                <span className="legend-color wall"></span> Wall
              </li>
              <li>
                <span className="legend-color pellet"></span> Pellet
              </li>
              <li>
                <span className="legend-color animal-spawn"></span> Animal Spawn
              </li>
              <li>
                <span className="legend-color zookeeper-spawn"></span> Zookeeper Spawn
              </li>
              <li>
                <span className="legend-color animal"></span> Animal
              </li>
              <li>
                <span className="legend-color zookeeper"></span> Zookeeper
              </li>
            </ul>
            {currentGameState && (
              <>
                <h4 className="legend-subheader">Animals:</h4>
                <ul className="legend-list">
                  {currentGameState.animals.map((a) => (
                    <li key={a.id}>
                      <span className="legend-color" style={{ backgroundColor: animalColorMap[a.id] }}></span> {a.nickname}
                    </li>
                  ))}
                </ul>
                <h4 className="legend-subheader">Zookeepers:</h4>
                <ul className="legend-list">
                  {currentGameState.zookeepers.map((z) => (
                    <li key={z.id}>
                      <span className="legend-color zookeeper"></span> {z.nickname}
                    </li>
                  ))}
                </ul>
              </>
            )}
          </div>
        </div>
      </div>
      {/* Leaderboard Section */}
      <div className="leaderboard-log-container log-leaderboard-section" style={{ clear: 'both', marginTop: '20px', padding: '10px', borderRadius: '5px', width: 'calc(100% - 20px)', margin: '20px auto 0 auto' }}>
        <h2 style={{ textAlign: 'center', marginBottom: '15px' }}>Leaderboard from Logs</h2>
        {leaderboardLoading ? (
          <div style={{ textAlign: 'center' }}>
            <div className="spinner" /> {/* Assuming you have a .spinner CSS class for animation */}
            <p>{leaderboardStatusMessage}</p>
          </div>
        ) : leaderboardData && leaderboardData.length > 0 ? (
          <Leaderboard animals={leaderboardData} />
        ) : (
          <p style={{ textAlign: 'center' }}>{leaderboardStatusMessage || 'No leaderboard data available.'}</p>
        )}
      </div>
    </div>
  );
};

export default App;
