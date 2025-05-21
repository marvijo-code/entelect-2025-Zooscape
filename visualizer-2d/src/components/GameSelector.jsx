import React, { useState, useEffect, useCallback } from 'react';
import '../styles/GameSelector.css';

const GameSelector = ({ onGameSelected, apiBaseUrl }) => {
  const [games, setGames] = useState([]);
  const [selectedGame, setSelectedGame] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  // Fetch available games on component mount
  useEffect(() => {
    fetchGames();
  }, []);

  const fetchGames = async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/games`);
      if (!response.ok) {
        throw new Error(`Failed to fetch games: ${response.statusText}`);
      }
      const data = await response.json();
      setGames(data.games || []);
    } catch (err) {
      setError(`Error loading games: ${err.message}`);
      console.error('Error fetching games:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleGameSelect = (game) => {
    setSelectedGame(game);
    loadGame(game.id);
  };

  const loadGame = async (gameId) => {
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/games/${gameId}`);
      if (!response.ok) {
        throw new Error(`Failed to fetch game data: ${response.statusText}`);
      }
      const gameData = await response.json();
      onGameSelected(gameData);
    } catch (err) {
      setError(`Error loading game data: ${err.message}`);
      console.error('Error fetching game data:', err);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="game-selector">
      <h2>Select Game to Replay</h2>
      
      {error && <div className="error-message">{error}</div>}
      
      <div className="games-list-container">
        {loading ? (
          <div className="loading-indicator">Loading games...</div>
        ) : games.length === 0 ? (
          <div className="no-games-message">No games available for replay</div>
        ) : (
          <ul className="games-list">
            {games.map(game => (
              <li 
                key={game.id} 
                className={`game-item ${selectedGame?.id === game.id ? 'selected' : ''}`}
                onClick={() => handleGameSelect(game)}
              >
                <div className="game-info">
                  <span className="game-name">{game.name || `Game ${game.id}`}</span>
                  <span className="game-date">{new Date(game.date).toLocaleString()}</span>
                </div>
                <div className="game-details">
                  <span>Players: {game.playerCount || 'N/A'}</span>
                  <span>Ticks: {game.tickCount || 'N/A'}</span>
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="refresh-container">
        <button 
          className="refresh-button"
          onClick={fetchGames}
          disabled={loading}
        >
          Refresh Games List
        </button>
      </div>
    </div>
  );
};

export default GameSelector; 