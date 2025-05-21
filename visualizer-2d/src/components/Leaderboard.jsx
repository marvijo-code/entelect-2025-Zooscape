import React from 'react';
import './Leaderboard.css'; // We'll create this for styling

const Leaderboard = ({ animals = [], leaderboardData = [], loading = false, statusMessage = '', colorMap = {} }) => {
  // Handle in-game animal scores display
  const renderAnimalScores = () => {
    if (!animals || animals.length === 0) return null;

    // Sort animals by score
    const sortedAnimals = [...animals].sort((a, b) => {
      const scoreA = a.score !== undefined ? a.score : a.Score;
      const scoreB = b.score !== undefined ? b.score : b.Score;
      return scoreB - scoreA;
    });

    return (
      <div className="game-scores">
        <h3>Current Game Scores</h3>
        <table className="leaderboard-table">
          <thead>
            <tr>
              <th>Rank</th>
              <th>Animal</th>
              <th>Score</th>
              <th>Captured</th>
              <th>Distance</th>
            </tr>
          </thead>
          <tbody>
            {sortedAnimals.map((animal, index) => {
              const animalId = animal.id !== undefined ? animal.id : animal.Id;
              
              // Enhanced nickname handling with better fallback
              let nickname = null;
              if (animal.nickname !== undefined) nickname = animal.nickname;
              else if (animal.Nickname !== undefined) nickname = animal.Nickname;
              else nickname = animalId ? `Bot-${animalId}` : `Unknown-${index}`;
              
              const score = animal.score !== undefined ? animal.score : animal.Score;
              const captured = animal.capturedCounter !== undefined ? animal.capturedCounter : animal.CapturedCounter;
              const distance = animal.distanceCovered !== undefined ? animal.distanceCovered : animal.DistanceCovered;
              
              return (
                <tr 
                  key={animalId || index} 
                  className="player-row"
                  style={{ backgroundColor: colorMap[animalId] ? `${colorMap[animalId]}20` : undefined }}
                >
                  <td>{index + 1}</td>
                  <td>
                    <span 
                      className="color-dot" 
                      style={{ backgroundColor: colorMap[animalId] || 'gray' }}
                    ></span>
                    {nickname}
                  </td>
                  <td>{score}</td>
                  <td>{captured}</td>
                  <td>{distance}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    );
  };

  // Handle tournament leaderboard display
  const renderLeaderboard = () => {
    if (!leaderboardData || leaderboardData.length === 0) {
      return null;
    }

    return (
      <div className="tournament-leaderboard">
        <h3>Tournament Leaderboard</h3>
        <table className="leaderboard-table">
          <thead>
            <tr>
              <th>Rank</th>
              <th>Bot Name</th>
              <th>Wins</th>
              <th>2nd Places</th>
              <th>Games</th>
            </tr>
          </thead>
          <tbody>
            {leaderboardData.map((bot, index) => {
              // Enhanced bot nickname handling
              let nickname = bot.nickname;
              if (!nickname && bot.id) {
                nickname = `Bot-${bot.id}`;
              } else if (!nickname) {
                nickname = `Player-${index + 1}`;
              }
              
              return (
                <tr key={bot.id || bot.nickname || index} className="player-row">
                  <td>{index + 1}</td>
                  <td>{nickname}</td>
                  <td>{bot.wins}</td>
                  <td>{bot.secondPlaces}</td>
                  <td>{bot.gamesPlayed}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    );
  };

  return (
    <div className="leaderboard-container">
      <h2>Scores & Leaderboard</h2>
      
      {loading && (
        <div className="leaderboard-loading">
          <div className="loading-spinner"></div>
          <p>{statusMessage || 'Loading...'}</p>
        </div>
      )}
      
      {!loading && renderAnimalScores()}
      
      {!loading && leaderboardData.length > 0 && (
        <div className="section-divider"></div>
      )}
      
      {!loading && renderLeaderboard()}
      
      {!loading && animals.length === 0 && leaderboardData.length === 0 && (
        <p className="no-data-message">
          {statusMessage || 'No score or leaderboard data available.'}
        </p>
      )}
    </div>
  );
};

export default Leaderboard;
