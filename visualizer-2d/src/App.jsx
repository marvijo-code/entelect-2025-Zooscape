import React, { useState, useEffect, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import Grid from './components/Grid.jsx';
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
  const animalColors = ['blue', 'green', 'orange', 'purple', 'cyan', 'magenta', 'yellow', 'lime'];

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
        setAnimalColorMap(history[0]?.animals.reduce((map, a, idx) => { map[a.id] = animalColors[idx % animalColors.length]; return map; }, {}));
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
        setAnimalColorMap(payload.animals.reduce((map, a, idx) => { map[a.id] = animalColors[idx % animalColors.length]; return map; }, {}));
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
    console.log("GameState received:", data);
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
          setAnimalColorMap(newGameState.animals.reduce((map, a, idx) => { map[a.id] = animalColors[idx % animalColors.length]; return map; }, {}));
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
            <p>{isConnected ? `Waiting for game data... (${allGameStates.length} loaded, idx ${currentDisplayIndex})` : 'Connecting...'}</p>
          )}
        </div>
        <div className="side-panel">
          <header className="App-header"><h1>2D Zooscape Visualizer</h1></header>
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
                <table><thead><tr><th>NickName</th><th>Score</th><th>Captured</th><th>Distance</th><th>Viable</th></tr></thead>
                  <tbody>{scoreboardData.map(animal => (<tr key={animal.id} style={{ backgroundColor: animalColorMap[animal.id] }}><td>{animal.nickname}</td><td>{animal.score}</td><td>{animal.capturedCounter}</td><td>{animal.distanceCovered}</td><td>{animal.isViable ? 'Yes' : 'No'}</td></tr>))}</tbody>
                </table>) : <p>No animal data.</p>}
            </div>
          )}
          <div className="controls">
            <div>
              <button onClick={handleReplay} disabled={allGameStates.length === 0}>Replay</button>
              <button onClick={handleRewind} disabled={!gameInitialized || currentDisplayIndex === 0 || isPlaying}>Rewind</button>
              <button onClick={handlePlayPause} disabled={!gameInitialized || isGameOver}>{isPlaying ? 'Pause' : 'Play'}</button>
              <button onClick={handleForward} disabled={!gameInitialized || currentDisplayIndex >= allGameStates.length - 1 || isPlaying}>Forward</button>
            </div>
            <div>
              <span>Tick: {currentGameState?.tick ?? 'N/A'} (Frame {currentDisplayIndex + 1}/{allGameStates.length})</span>
              <span style={{ marginLeft: '10px', fontSize: '0.8em', color: '#555' }}>States: {allGameStates.length}, Index: {currentDisplayIndex}</span>
              {isGameOver && <span className="game-over-text">GAME OVER</span>}
            </div>
          </div>
          <div className="legend">
            <h3 className="legend-header">Legend</h3>
            <ul className="legend-list">
              <li><span className="legend-color wall"></span> Wall</li>
              <li><span className="legend-color pellet"></span> Pellet</li>
              <li><span className="legend-color animal-spawn"></span> Animal Spawn</li>
              <li><span className="legend-color zookeeper-spawn"></span> Zookeeper Spawn</li>
              <li><span className="legend-color animal"></span> Animal</li>
              <li><span className="legend-color zookeeper"></span> Zookeeper</li>
            </ul>
            {currentGameState && (
              <>
                <h4 className="legend-subheader">Animals:</h4>
                <ul className="legend-list">
                  {currentGameState.animals.map(a => (
                    <li key={a.id}><span className="legend-color" style={{ backgroundColor: animalColorMap[a.id] }}></span> {a.nickname}</li>
                  ))}
                </ul>
                <h4 className="legend-subheader">Zookeepers:</h4>
                <ul className="legend-list">
                  {currentGameState.zookeepers.map(z => (
                    <li key={z.id}><span className="legend-color zookeeper"></span> {z.nickname}</li>
                  ))}
                </ul>
              </>
            )}
          </div>
        </div>
      </div>
    </div>);
};
export default App;
