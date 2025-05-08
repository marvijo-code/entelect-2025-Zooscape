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

  const { Cells, Animals, Zookeepers } = gameState;

  const maxX = Cells && Cells.length > 0 ? Math.max(...Cells.map(c => c.X), 0) : 10;
  const maxY = Cells && Cells.length > 0 ? Math.max(...Cells.map(c => c.Y), 0) : 10;
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
      {Cells && Cells.map((cell, index) => (
        <div
          key={`cell-${cell.X}-${cell.Y}-${index}`}
          style={{
            position: 'absolute', left: cell.X * TILE_SIZE, top: cell.Y * TILE_SIZE,
            width: TILE_SIZE, height: TILE_SIZE,
            backgroundColor: getCellColor(cell.Content),
            border: '1px solid #ddd', boxSizing: 'border-box',
          }}
        />
      ))}
      {/* Render Animals */}
      {Animals && Animals.map(animal => (
        <div
          key={`animal-${animal.Id}`} title={`${animal.NickName} (Animal)`}
          style={{
            position: 'absolute', left: animal.X * TILE_SIZE + TILE_SIZE / 4, top: animal.Y * TILE_SIZE + TILE_SIZE / 4,
            width: TILE_SIZE / 2, height: TILE_SIZE / 2,
            backgroundColor: 'blue', borderRadius: '50%', textAlign: 'center',
            lineHeight: `${TILE_SIZE / 2}px`, fontSize: '10px', color: 'white', zIndex: 2, boxShadow: '0 0 2px black',
          }}
        >A</div>
      ))}
      {/* Render Zookeepers */}
      {Zookeepers && Zookeepers.map(zookeeper => (
        <div
          key={`zookeeper-${zookeeper.Id}`} title={`${zookeeper.NickName} (Zookeeper)`}
          style={{
            position: 'absolute', left: zookeeper.X * TILE_SIZE + TILE_SIZE / 4, top: zookeeper.Y * TILE_SIZE + TILE_SIZE / 4,
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
