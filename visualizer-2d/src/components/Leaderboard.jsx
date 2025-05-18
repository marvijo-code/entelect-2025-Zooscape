import React from 'react';
import './Leaderboard.css'; // We'll create this for styling

const Leaderboard = ({ animals }) => {
  if (!animals || animals.length === 0) {
    return <div className="leaderboard-container"><p>No leaderboard data available. Load a log file to see scores.</p></div>;
  }

  // Sort animals by score in descending order
  // Sort by Wins (descending), then by Score (descending) as a tie-breaker
  const sortedAnimals = [...animals].sort((a, b) => {
    if ((b.Wins || 0) !== (a.Wins || 0)) {
      return (b.Wins || 0) - (a.Wins || 0);
    }
    return (b.Score || 0) - (a.Score || 0);
  });

  return (
    <div className="leaderboard-container">
      <h2>Leaderboard</h2>
      <table className="leaderboard-table">
        <thead>
          <tr>
            <th>Rank</th>
            <th>Bot Name</th>
            <th>Last Score</th>
            <th>Win Stats (W/P)</th>
          </tr>
        </thead>
        <tbody>
          {sortedAnimals.map((animal, index) => (
            <tr key={animal.Id || index}>
              <td>{index + 1}</td>
              <td>{animal.Nickname || animal.NickName}</td>
              <td>{animal.Score !== undefined && animal.Score !== null ? animal.Score : 'N/A'}</td>
              <td>{animal.Wins !== undefined ? `${animal.Wins} / ${animal.TotalGamesParticipated}` : 'N/A'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};

export default Leaderboard;
