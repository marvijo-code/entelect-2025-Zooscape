import React, { useState, useEffect, useCallback, useMemo } from 'react';
import '../styles/GameSelector.css';

const GameSelector = React.memo(({ onGameSelected, apiBaseUrl }) => {
  const [games, setGames] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const fetchGames = useCallback(async () => {
    setLoading(true);
    setError(null);
    
    try {
      const response = await fetch(`${apiBaseUrl}/games`);
      if (!response.ok) {
        throw new Error(`Failed to fetch games: ${response.status}`);
      }
      
      const data = await response.json();
      setGames(data.games || []);
    } catch (err) {
      console.error('Error fetching games:', err);
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }, [apiBaseUrl]);

  useEffect(() => {
    fetchGames();
  }, [fetchGames]);

  const handleGameSelect = useCallback(async (gameId) => {
    setLoading(true);
    setError(null);
    
    try {
      const response = await fetch(`${apiBaseUrl}/games/${gameId}`);
      if (!response.ok) {
        throw new Error(`Failed to load game: ${response.status}`);
      }
      
      const gameData = await response.json();
      onGameSelected(gameData);
    } catch (err) {
      console.error('Error loading game:', err);
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }, [apiBaseUrl, onGameSelected]);

  // Memoize the game list to prevent unnecessary re-renders
  const gameList = useMemo(() => {
    if (!games || games.length === 0) {
      return (
        <div className="no-games-message">
          <p>No games available for replay.</p>
          <button onClick={fetchGames} className="retry-button">
            Retry
          </button>
        </div>
      );
    }

    return games.map((game) => (
      <div key={game.id} className="game-item">
        <div className="game-info">
          <h4>{game.name}</h4>
          <div className="game-details">
            <span>Players: {game.playerCount || 'Unknown'}</span>
            <span>Ticks: {game.tickCount || 'Unknown'}</span>
            <span>Date: {new Date(game.date).toLocaleDateString()}</span>
          </div>
        </div>
        <button 
          onClick={() => handleGameSelect(game.id)}
          className="select-game-button"
          disabled={loading}
        >
          {loading ? 'Loading...' : 'Load Game'}
        </button>
      </div>
    ));
  }, [games, loading, fetchGames, handleGameSelect]);

  return (
    <div className="game-selector">
      <div className="selector-header">
        <h3>Select Game to Replay</h3>
        <button 
          onClick={fetchGames} 
          className="refresh-games-button"
          disabled={loading}
          title="Refresh game list"
        >
          {loading ? '🔄' : '↻'} Refresh
        </button>
      </div>
      
      {error && (
        <div className="error-message">
          <strong>Error:</strong> {error}
        </div>
      )}
      
      {loading && games.length === 0 ? (
        <div className="loading-indicator">
          <div className="spinner"></div>
          <p>Loading available games...</p>
        </div>
      ) : (
        <div className="games-list">
          {gameList}
        </div>
      )}
    </div>
  );
});

GameSelector.displayName = 'GameSelector';

export default GameSelector; 