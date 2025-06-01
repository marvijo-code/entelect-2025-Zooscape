import React, { useMemo } from 'react';
import './Leaderboard.css'; // We'll create this for styling

const Leaderboard = React.memo(({ data = [], loading = false, statusMessage = '', onRefresh }) => {
  // Memoize the leaderboard rows to prevent unnecessary re-renders
  const leaderboardRows = useMemo(() => {
    if (!data || data.length === 0) {
      return null;
    }

    return data.map((bot, index) => (
      <tr key={bot.nickname || bot.id || index} className="leaderboard-row">
        <td className="rank-cell">{index + 1}</td>
        <td className="nickname-cell">{bot.nickname || `Bot-${bot.id}`}</td>
        <td className="wins-cell">{bot.wins || 0}</td>
        <td className="second-places-cell">{bot.secondPlaces || 0}</td>
        <td className="games-played-cell">{bot.gamesPlayed || 0}</td>
        <td className="win-rate-cell">
          {bot.gamesPlayed > 0 ? `${((bot.wins / bot.gamesPlayed) * 100).toFixed(1)}%` : '0%'}
        </td>
      </tr>
    ));
  }, [data]);

  return (
    <div className="leaderboard-container">
      <div className="leaderboard-header">
        <h2>Bot Leaderboard</h2>
        <button 
          onClick={onRefresh} 
          className="refresh-button"
          disabled={loading}
          title="Refresh leaderboard data"
        >
          {loading ? '🔄' : '↻'} Refresh
        </button>
      </div>
      
      {statusMessage && (
        <div className={`status-message ${loading ? 'loading' : ''}`}>
          {statusMessage}
        </div>
      )}
      
      {loading ? (
        <div className="loading-indicator">
          <div className="spinner"></div>
          <p>Loading leaderboard data...</p>
        </div>
      ) : data && data.length > 0 ? (
        <div className="leaderboard-table-container">
          <table className="leaderboard-table">
            <thead>
              <tr>
                <th>Rank</th>
                <th>Bot Name</th>
                <th>Wins</th>
                <th>2nd Places</th>
                <th>Games Played</th>
                <th>Win Rate</th>
              </tr>
            </thead>
            <tbody>
              {leaderboardRows}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="no-data-message">
          <p>No leaderboard data available.</p>
          <p>Play some games to see statistics here.</p>
        </div>
      )}
    </div>
  );
});

Leaderboard.displayName = 'Leaderboard';

export default Leaderboard;
