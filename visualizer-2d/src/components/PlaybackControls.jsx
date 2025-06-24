import React, { useState, useEffect } from 'react';
import './PlaybackControls.css';

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
  isFetchingTick,
  isSingleFrameView = false
}) => {
  const [speed, setSpeed] = useState(1.0);
  const [tickInput, setTickInput] = useState('');
  
  // Update tickInput when currentFrame changes (e.g., from slider or other controls)
  useEffect(() => {
    setTickInput((currentFrame + 1).toString());
  }, [currentFrame]);
  
  const handleSpeedChange = (newSpeed) => {
    setSpeed(newSpeed);
    onSpeedChange(newSpeed);
  };
  
  const handleSliderChange = (e) => {
    const value = parseInt(e.target.value, 10);
    onSetFrame(value);
  };

  const handleTickInputChange = (e) => {
    setTickInput(e.target.value);
  };

  const handleTickInputSubmit = (e) => {
    e.preventDefault();
    const tickNumber = parseInt(tickInput, 10);
    
    // Validate input
    if (isNaN(tickNumber) || tickNumber < 1 || tickNumber > totalFrames) {
      // Reset to current frame if invalid
      setTickInput((currentFrame + 1).toString());
      return;
    }
    
    // Navigate to the specified tick (convert from 1-based to 0-based)
    onSetFrame(tickNumber - 1);
  };

  const handleTickInputKeyDown = (e) => {
    if (e.key === 'Enter') {
      handleTickInputSubmit(e);
    } else if (e.key === 'Escape') {
      // Reset to current frame on escape
      setTickInput((currentFrame + 1).toString());
      e.target.blur();
    }
  };

  return (
    <div className={`playback-controls-container ${isSingleFrameView ? 'single-frame-view' : ''}`}>
      <div className="controls-row">
        <button onClick={onRestart} disabled={isSingleFrameView} className="control-button" title="Restart">
          ⏮
        </button>
        <button 
          onClick={onRewind} 
          className="control-button" 
          title="Previous frame"
          disabled={isFetchingTick || isSingleFrameView}
        >
          ⏪
        </button>
        <button 
          onClick={onPlayPause} 
          className="control-button play-pause-button" 
          title={isPlaying ? "Pause" : "Play"}
          disabled={isFetchingTick || isSingleFrameView}
        >
          {isPlaying ? '⏸' : '▶'}
        </button>
        <button 
          onClick={onForward} 
          className="control-button" 
          title="Next frame"
          disabled={isFetchingTick || isSingleFrameView}
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
            disabled={isFetchingTick || isSingleFrameView}
          />
        </div>
        
        <div className="tick-navigation">
          <form onSubmit={handleTickInputSubmit} className="tick-input-form">
            <input
              type="number"
              className="tick-input"
              value={tickInput}
              onChange={handleTickInputChange}
              onKeyDown={handleTickInputKeyDown}
              min="1"
              max={totalFrames}
              disabled={isFetchingTick || isSingleFrameView}
              title="Enter tick number and press Enter"
              placeholder="Tick"
            />
          </form>
          <span className="frame-counter">
            / {totalFrames}
          </span>
        </div>
        
        <div className="speed-controls">
          <select 
            value={speed} 
            onChange={(e) => handleSpeedChange(parseFloat(e.target.value))}
            className="speed-selector"
            disabled={isSingleFrameView}
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