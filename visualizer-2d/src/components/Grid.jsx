import React, { useEffect, useState, useRef } from 'react';
// Import from the new JavaScript models file
import { CellContent } from '../models.js'; 

// JSDoc for props instead of TypeScript interface
/**
 * @typedef {import('../models.js').GameState} GameState
 */

/**
 * @param {object} props
 * @param {GameState | null} props.gameState
 * @param {object} props.colorMap
 */

const Grid = ({ gameState, colorMap = {} }) => {
  const containerRef = useRef(null);
  const [tileSize, setTileSize] = useState(15); // Default tile size
  const [containerSize, setContainerSize] = useState({ width: 0, height: 0 });

  if (!gameState) {
    return <div>Loading game state...</div>;
  }

  const { cells, animals, zookeepers } = gameState;

  const maxX = cells && cells.length > 0 ? Math.max(...cells.map(c => c.x), 0) : 10;
  const maxY = cells && cells.length > 0 ? Math.max(...cells.map(c => c.y), 0) : 10;
  
  // Calculate the optimal tile size based on container dimensions
  useEffect(() => {
    if (!containerRef.current) return;
    
    const updateSize = () => {
      const containerWidth = containerRef.current.clientWidth;
      const containerHeight = containerRef.current.clientHeight;
      
      setContainerSize({
        width: containerWidth,
        height: containerHeight
      });
      
      // Calculate the maximum possible tile size that fits the grid
      const maxTileWidth = containerWidth / (maxX + 1);
      const maxTileHeight = containerHeight / (maxY + 1);
      const newTileSize = Math.floor(Math.min(maxTileWidth, maxTileHeight, 30)); // Cap at 30px
      
      setTileSize(Math.max(newTileSize, 8)); // Minimum tile size of 8px
    };
    
    // Initial size calculation
    updateSize();
    
    // Add resize observer to handle container size changes
    const resizeObserver = new ResizeObserver(updateSize);
    resizeObserver.observe(containerRef.current);
    
    // Cleanup
    return () => {
      if (containerRef.current) {
        resizeObserver.unobserve(containerRef.current);
      }
      resizeObserver.disconnect();
    };
  }, [maxX, maxY]);
  
  const gridWidth = (maxX + 1) * tileSize;
  const gridHeight = (maxY + 1) * tileSize;

  // Remove type annotation from 'content' parameter
  const getCellColor = (content) => {
    switch (content) {
      case CellContent.Wall: return 'grey';
      case CellContent.Pellet: return 'yellow';
      case CellContent.AnimalSpawn: return 'lightblue';
      case CellContent.ZookeeperSpawn: return 'lightcoral';
      case CellContent.Empty: default: return 'white';
    }
  };

  return (
    <div ref={containerRef} style={{ width: '100%', height: '100%', display: 'flex', justifyContent: 'center', alignItems: 'center', overflow: 'hidden' }}>
      <div style={{ 
        position: 'relative', 
        width: gridWidth, 
        height: gridHeight, 
        border: '1px solid black', 
        backgroundColor: '#f0f0f0',
        margin: 'auto'
      }}>
        {/* Render Cells */}
        {cells && cells.map((cell, index) => (
          <div
            key={`cell-${cell.x}-${cell.y}-${index}`}
            style={{
              position: 'absolute', left: cell.x * tileSize, top: cell.y * tileSize,
              width: tileSize, height: tileSize,
              backgroundColor: getCellColor(cell.content),
              border: '1px solid #ddd', boxSizing: 'border-box',
            }}
          />
        ))}
        {/* Render Animals */}
        {animals && animals.map(animal => {
          const fontSize = Math.max(Math.floor(tileSize / 3), 8);
          return (
            <div
              key={`animal-${animal.id}`} title={`${animal.nickname} (Animal)`}
              style={{
                position: 'absolute', 
                left: animal.x * tileSize + tileSize / 4, 
                top: animal.y * tileSize + tileSize / 4,
                width: tileSize / 2, 
                height: tileSize / 2,
                backgroundColor: colorMap[animal.id] || 'white', 
                borderRadius: '50%', 
                textAlign: 'center',
                lineHeight: `${tileSize / 2}px`, 
                fontSize: `${fontSize}px`, 
                color: 'black', 
                zIndex: 2, 
                boxShadow: '0 0 2px black',
                overflow: 'hidden'
              }}
            >{tileSize > 15 ? animal.nickname : ''}</div>
          );
        })}
        {/* Render Zookeepers */}
        {zookeepers && zookeepers.map(zookeeper => {
          const fontSize = Math.max(Math.floor(tileSize / 3), 8);
          return (
            <div
              key={`zookeeper-${zookeeper.id}`} title={`${zookeeper.nickname} (Zookeeper)`}
              style={{
                position: 'absolute', 
                left: zookeeper.x * tileSize + tileSize / 4, 
                top: zookeeper.y * tileSize + tileSize / 4,
                width: tileSize / 2, 
                height: tileSize / 2,
                backgroundColor: 'red', 
                borderRadius: '20%', 
                textAlign: 'center',
                lineHeight: `${tileSize / 2}px`, 
                fontSize: `${fontSize}px`, 
                color: 'black', 
                zIndex: 2, 
                boxShadow: '0 0 2px black',
                overflow: 'hidden'
              }}
            >{tileSize > 15 ? zookeeper.nickname : ''}</div>
          );
        })}
      </div>
    </div>
  );
};

export default Grid;
