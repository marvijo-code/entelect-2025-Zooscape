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
  onExitReplay
}) => {
  const [speed, setSpeed] = useState(1.0);
  const [displayTime, setDisplayTime] = useState('00:00');
  
  // Calculate time display (MM:SS) based on frame number and speed
  useEffect(() => {
    const frameDuration = 1000 / (speed * 8); // Assuming 8 frames per second at normal speed
    const totalTimeMs = currentFrame * frameDuration;
    const minutes = Math.floor(totalTimeMs / 60000);
    const seconds = Math.floor((totalTimeMs % 60000) / 1000);
    setDisplayTime(`${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`);
  }, [currentFrame, speed]);
  
  const handleSpeedChange = (newSpeed) => {
    setSpeed(newSpeed);
    onSpeedChange(newSpeed);
  };
  
  const handleFrameInput = (e) => {
    const value = parseInt(e.target.value, 10);
    if (!isNaN(value) && value >= 0 && value < totalFrames) {
      onSetFrame(value);
    }
  };

  const handleSliderChange = (e) => {
    const value = parseInt(e.target.value, 10);
    onSetFrame(value);
  };

  const progressPercentage = totalFrames > 0 ? (currentFrame / (totalFrames - 1)) * 100 : 0;

  return (
    <div className="playback-controls">
      <div className="playback-info">
        <span className="frame-counter">Frame: {currentFrame + 1}/{totalFrames}</span>
        <span className="time-display">{displayTime}</span>
      </div>
      
      <div className="progress-container">
        <input
          type="range"
          className="progress-slider"
          min="0"
          max={totalFrames > 0 ? totalFrames - 1 : 0}
          value={currentFrame}
          onChange={handleSliderChange}
        />
      </div>
      
      <div className="controls-row">
        <button onClick={onRestart} className="control-button restart-button" title="Restart">
          <i className="fas fa-step-backward"></i>
        </button>
        <button onClick={onRewind} className="control-button" title="Previous frame">
          <i className="fas fa-backward"></i>
        </button>
        <button onClick={onPlayPause} className="control-button play-pause-button" title={isPlaying ? "Pause" : "Play"}>
          <i className={`fas ${isPlaying ? 'fa-pause' : 'fa-play'}`}></i>
        </button>
        <button onClick={onForward} className="control-button" title="Next frame">
          <i className="fas fa-forward"></i>
        </button>
        
        <div className="speed-controls">
          <span>Speed:</span>
          <select 
            value={speed} 
            onChange={(e) => handleSpeedChange(parseFloat(e.target.value))}
            className="speed-selector"
          >
            <option value="0.25">0.25x</option>
            <option value="0.5">0.5x</option>
            <option value="1">1x</option>
            <option value="1.5">1.5x</option>
            <option value="2">2x</option>
            <option value="4">4x</option>
          </select>
        </div>
      </div>
      
      <button onClick={onExitReplay} className="exit-replay-button">
        Exit Replay
      </button>
    </div>
  );
};

export default PlaybackControls; 