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
        console.log("Game initialized with StartGame data (flat structure).");
      } else {
        console.error("StartGame: Invalid payload structure. Expected {cells, animals, zookeepers}", payload);
        setError("Failed to initialize game: Invalid data from server for StartGame.");
      }
    } catch (e) { 
      console.error("Error parsing StartGame data:", e, "Raw data:", data);
      setError(`Failed to initialize game: ${e.message}`);
    }
  }, [setAllGameStates, setCurrentDisplayIndex, setGameInitialized, setIsPlaying, setIsGameOver, setError]);

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
        }

        setAllGameStates(prevStates => {
          // Check if this tick is already processed or is older than current
          // This basic check prevents duplicate processing if messages arrive out of order or get re-sent
          // A more robust solution might involve comparing newGameState.tick with the tick of the last state in prevStates
          if (prevStates.length > 0 && newGameState.tick <= prevStates[prevStates.length - 1].tick) {
            // console.warn(`Skipping older or duplicate GameState tick: ${newGameState.tick}`);
            // return prevStates; // Or handle updates to existing states if necessary
          }

          const updatedStates = [...prevStates, newGameState];
          setCurrentDisplayIndex(updatedStates.length - 1); // Always show the latest received state
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
  }, [gameInitialized, setGameInitialized, setIsPlaying, setIsGameOver, setAllGameStates, setCurrentDisplayIndex, setError]);

  const gameOverHandler = useCallback((message) => {
    console.log("GameOver received:", message);
    setIsGameOver(true);
    setIsPlaying(false);
  }, [setIsGameOver, setIsPlaying]);

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
    if (!isConnected || isReplaying) {
        if (!isConnected) setError("Cannot replay: Not connected.");
        if (isReplaying) console.log("Replay already in progress...");
        return;
    }

    console.log("Attempting replay...");
    setIsReplaying(true);
    setAllGameStates([]); 
    setGameInitialized(false); 
    setIsGameOver(false);
    setCurrentDisplayIndex(0);
    setIsPlaying(true);
    connection.invoke("RegisterVisualiser")
      .then(() => {
        console.log("Visualiser re-registered for replay");
        setIsReplaying(false);
      })
      .catch(err => {
          console.error("Error re-registering:", err);
          setError("Error trying to replay.");
          setIsReplaying(false);
      });
  };

  const handleRewind = () => { setIsPlaying(false); setCurrentDisplayIndex(prev => Math.max(0, prev - 1)); };
  const handlePlayPause = () => {
     const newIsPlaying = !isPlaying;
     setIsPlaying(newIsPlaying);
     if (newIsPlaying && allGameStates.length > 0 && currentDisplayIndex < allGameStates.length - 1 && !isGameOver) {
       setCurrentDisplayIndex(allGameStates.length - 1); // Jump to live view if playing
     }
  };
  const handleForward = () => { setIsPlaying(false); setCurrentDisplayIndex(prev => Math.min(allGameStates.length - 1, prev + 1)); };
  
  const currentGameState = allGameStates[currentDisplayIndex] || null;
  const scoreboardData = currentGameState?.animals ? [...currentGameState.animals].sort((a, b) => b.Score - a.Score) : [];

  return (
    <div className="App">
      <header className="App-header"><h1>2D Zooscape Visualizer</h1></header>
      {error && <div className="error-banner">Error: {error}</div>}
      <div className="controls">
        <button onClick={handleReplay} disabled={!isConnected || isReplaying}>Replay</button>
        <button onClick={handleRewind} disabled={!gameInitialized || currentDisplayIndex === 0 || isPlaying}>Rewind</button>
        <button onClick={handlePlayPause} disabled={!gameInitialized || isGameOver}>{isPlaying ? 'Pause' : 'Play'}</button>
        <button onClick={handleForward} disabled={!gameInitialized || currentDisplayIndex >= allGameStates.length - 1 || isPlaying}>Forward</button>
        <span>Tick: {currentGameState?.tick ?? 'N/A'} (Frame {currentDisplayIndex + 1}/{allGameStates.length})</span>
        {isGameOver && <span className="game-over-text">GAME OVER</span>}
      </div>
      <div className="main-content">
        <div className="grid-container">
          {currentGameState ? (
            <Grid gameState={currentGameState} />
          ) : (
            <p>{isConnected ? 'Waiting for game data...' : 'Connecting...'}</p>
          )}
        </div>
        {gameInitialized && (
          <div className="scoreboard-container">
            <h2>Scoreboard</h2>
            {scoreboardData.length > 0 ? (
              <table><thead><tr><th>NickName</th><th>Score</th><th>Captured</th><th>Distance</th><th>Viable</th></tr></thead>
              <tbody>{scoreboardData.map(animal => (<tr key={animal.Id}><td>{animal.NickName}</td><td>{animal.Score}</td><td>{animal.CapturedCounter}</td><td>{animal.DistanceCovered}</td><td>{animal.IsViable ? 'Yes' : 'No'}</td></tr>))}</tbody>
              </table>) : <p>No animal data.</p>}
          </div>
        )}
      </div>
    </div>);
};
export default App;
