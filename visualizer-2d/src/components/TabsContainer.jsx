import React from 'react';

const TabsContainer = React.memo(({ activeTabIndex, onTabChange, showReplayMode }) => {
  return (
    <>
      <button 
        className={`tab-button ${activeTabIndex === 0 ? 'active' : ''}`}
        onClick={() => onTabChange(0)}
      >
        Leaderboard
      </button>
      
      {showReplayMode ? (
        <>
          <button 
            className={`tab-button ${activeTabIndex === 1 ? 'active' : ''}`}
            onClick={() => onTabChange(1)}
          >
            Game Selector
          </button>
          <button 
            className={`tab-button ${activeTabIndex === 2 ? 'active' : ''}`}
            onClick={() => onTabChange(2)}
          >
            Test Runner
          </button>
        </>
      ) : (
        <button 
          className={`tab-button ${activeTabIndex === 3 ? 'active' : ''}`}
          onClick={() => onTabChange(3)}
        >
          Connection
        </button>
      )}
    </>
  );
});

TabsContainer.displayName = 'TabsContainer';

export default TabsContainer; 