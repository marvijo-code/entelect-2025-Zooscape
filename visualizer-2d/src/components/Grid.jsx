import React from 'react';
// Import from the new JavaScript models file
import { CellContent } from '../models.js'; 

// JSDoc for props instead of TypeScript interface
/**
 * @typedef {import('../models.js').GameState} GameState
 */

/**
 * @param {object} props
 * @param {GameState | null} props.gameState
 */

const TILE_SIZE = 20; // pixels

// Remove React.FC<GridProps> type annotation
const Grid = ({ gameState }) => {
  if (!gameState) {
    return <div>Loading game state...</div>;
  }

  const { cells, animals, zookeepers } = gameState;

  const maxX = cells && cells.length > 0 ? Math.max(...cells.map(c => c.x), 0) : 10;
  const maxY = cells && cells.length > 0 ? Math.max(...cells.map(c => c.y), 0) : 10;
  const gridWidth = (maxX + 1) * TILE_SIZE;
  const gridHeight = (maxY + 1) * TILE_SIZE;

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
    <div style={{ position: 'relative', width: gridWidth, height: gridHeight, border: '1px solid black', backgroundColor: '#f0f0f0' }}>
      {/* Render Cells */}
      {cells && cells.map((cell, index) => (
        <div
          key={`cell-${cell.x}-${cell.y}-${index}`}
          style={{
            position: 'absolute', left: cell.x * TILE_SIZE, top: cell.y * TILE_SIZE,
            width: TILE_SIZE, height: TILE_SIZE,
            backgroundColor: getCellColor(cell.content),
            border: '1px solid #ddd', boxSizing: 'border-box',
          }}
        />
      ))}
      {/* Render Animals */}
      {animals && animals.map(animal => (
        <div
          key={`animal-${animal.id}`} title={`${animal.nickName} (Animal)`}
          style={{
            position: 'absolute', left: animal.x * TILE_SIZE + TILE_SIZE / 4, top: animal.y * TILE_SIZE + TILE_SIZE / 4,
            width: TILE_SIZE / 2, height: TILE_SIZE / 2,
            backgroundColor: 'blue', borderRadius: '50%', textAlign: 'center',
            lineHeight: `${TILE_SIZE / 2}px`, fontSize: '10px', color: 'white', zIndex: 2, boxShadow: '0 0 2px black',
          }}
        >A</div>
      ))}
      {/* Render Zookeepers */}
      {zookeepers && zookeepers.map(zookeeper => (
        <div
          key={`zookeeper-${zookeeper.id}`} title={`${zookeeper.nickName} (Zookeeper)`}
          style={{
            position: 'absolute', left: zookeeper.x * TILE_SIZE + TILE_SIZE / 4, top: zookeeper.y * TILE_SIZE + TILE_SIZE / 4,
            width: TILE_SIZE / 2, height: TILE_SIZE / 2,
            backgroundColor: 'red', borderRadius: '20%', textAlign: 'center',
            lineHeight: `${TILE_SIZE / 2}px`, fontSize: '10px', color: 'white', zIndex: 2, boxShadow: '0 0 2px black',
          }}
        >Z</div>
      ))}
    </div>
  );
};

export default Grid;
