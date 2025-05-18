import React from 'react';
import './Leaderboard.css'; // We'll create this for styling

const Leaderboard = ({ leaderboardData }) => {
  if (!leaderboardData || leaderboardData.length === 0) {
    return <div className="leaderboard-container"><p>No leaderboard data available. Load a log file to see scores.</p></div>;
  }

  // Data is pre-sorted by the API
  return (
    <div className="leaderboard-container">
      <h2>Leaderboard</h2>
      <table className="leaderboard-table">
        <thead>
          <tr>
            <th>Rank</th>
            <th>Bot Name</th>
            <th>Wins</th>
            <th>2nd Places</th>
            <th>Games Played</th>
          </tr>
        </thead>
        <tbody>
          {leaderboardData.map((bot, index) => (
            <tr key={bot.nickname || index}> {/* Assuming nickname is unique for key, or use index as fallback */}
              <td>{index + 1}</td>
              <td>{bot.nickname}</td>
              <td>{bot.wins}</td>
              <td>{bot.secondPlaces}</td>
              <td>{bot.gamesPlayed}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};

export default Leaderboard;
