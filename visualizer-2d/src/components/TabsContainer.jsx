import React from 'react';

const TabsContainer = ({ tabs, activeTabIndex = 0, onTabChange }) => {
  const handleTabClick = (index) => {
    if (onTabChange) {
      onTabChange(index);
    }
  };

  return (
    <div className="tabs-container">
      <div className="tabs-header">
        {tabs.map((tab, index) => (
          <button
            key={index}
            className={`tab-button ${activeTabIndex === index ? 'active' : ''}`}
            onClick={() => handleTabClick(index)}
          >
            {tab.label}
          </button>
        ))}
      </div>
      <div className="tab-content">
        {tabs[activeTabIndex].content}
      </div>
    </div>
  );
};

export default TabsContainer; 