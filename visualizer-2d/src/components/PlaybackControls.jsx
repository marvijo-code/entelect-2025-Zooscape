import React, { useState, useEffect } from 'react';
import '../styles/PlaybackControls.css';

const PlaybackControls = ({
  currentFrame,
  totalFrames,
  isPlaying,
  onPlayPause,
  onRewind,
  onForward,
  onSetFrame,
  onSpeedChange,
  onRestart,
  onExitReplay,
  isFetchingTick
}) => {
  const [speed, setSpeed] = useState(1.0);
  
  const handleSpeedChange = (newSpeed) => {
    setSpeed(newSpeed);
    onSpeedChange(newSpeed);
  };
  
  const handleSliderChange = (e) => {
    const value = parseInt(e.target.value, 10);
    onSetFrame(value);
  };

  return (
    <div className="playback-controls">
      <div className="controls-row">
        <button onClick={onRestart} className="control-button" title="Restart">
          ⏮
        </button>
        <button 
          onClick={onRewind} 
          className="control-button" 
          title="Previous frame"
          disabled={isFetchingTick}
        >
          ⏪
        </button>
        <button 
          onClick={onPlayPause} 
          className="control-button play-pause-button" 
          title={isPlaying ? "Pause" : "Play"}
          disabled={isFetchingTick}
        >
          {isPlaying ? '⏸' : '▶'}
        </button>
        <button 
          onClick={onForward} 
          className="control-button" 
          title="Next frame"
          disabled={isFetchingTick}
        >
          ⏩
        </button>
        
        <div className="progress-container">
          <input
            type="range"
            className="progress-slider"
            min="0"
            max={totalFrames > 0 ? totalFrames - 1 : 0}
            value={currentFrame}
            onChange={handleSliderChange}
            disabled={isFetchingTick}
          />
        </div>
        
        <span className="frame-counter">
          {isFetchingTick ? (
            <span className="loading-indicator">Loading...</span>
          ) : (
            `${currentFrame + 1}/${totalFrames}`
          )}
        </span>
        
        <div className="speed-controls">
          <select 
            value={speed} 
            onChange={(e) => handleSpeedChange(parseFloat(e.target.value))}
            className="speed-selector"
          >
            <option value="0.25">0.25x</option>
            <option value="0.5">0.5x</option>
            <option value="1">1x</option>
            <option value="2">2x</option>
            <option value="4">4x</option>
          </select>
        </div>
        
        <button onClick={onExitReplay} className="exit-button" title="Exit Replay">
          ✕
        </button>
      </div>
    </div>
  );
};

export default PlaybackControls; 