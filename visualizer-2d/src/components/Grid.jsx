import React, { useEffect, useState, useRef } from 'react';
// Import from the new JavaScript models file
import { CellContent } from '../models.js'; 

/**
 * @param {object} props
 * @param {Array} props.cells - Array of cell objects
 * @param {Array} props.animals - Array of animal objects
 * @param {Array} props.zookeepers - Array of zookeeper objects
 * @param {object} props.colorMap - Map of animal IDs to colors
 */
const Grid = ({ cells = [], animals = [], zookeepers = [], colorMap = {} }) => {
  const containerRef = useRef(null);
  const [tileSize, setTileSize] = useState(15); // Default tile size
  const [containerSize, setContainerSize] = useState({ width: 0, height: 0 });

  console.log("Grid render with:", { 
    cellsLength: cells.length, 
    animalsLength: animals.length, 
    zookeepersLength: zookeepers.length 
  });

  if (!cells || cells.length === 0) {
    return (
      <div className="grid-loading">
        <p>Waiting for grid data...</p>
        <p className="debug-info">No cell data available. Check server connection or game state.</p>
      </div>
    );
  }

  // Get cell coordinates - handle both lowercase and uppercase property names
  const getCellX = (cell) => cell.x !== undefined ? cell.x : cell.X;
  const getCellY = (cell) => cell.y !== undefined ? cell.y : cell.Y;
  
  // Get entity coordinates - handle both lowercase and uppercase property names
  const getEntityX = (entity) => entity.x !== undefined ? entity.x : entity.X;
  const getEntityY = (entity) => entity.y !== undefined ? entity.y : entity.Y;
  
  // Get entity ID - handle both lowercase and uppercase property names
  const getEntityId = (entity) => entity.id !== undefined ? entity.id : entity.Id;
  
  // Get entity nickname - handle both lowercase and uppercase property names
  const getEntityNickname = (entity) => {
    return entity.nickname !== undefined ? entity.nickname : 
           entity.Nickname !== undefined ? entity.Nickname : 
           `Entity-${getEntityId(entity) || 'unknown'}`;
  };
  
  // Get cell content - handle both lowercase and uppercase property names
  const getCellContent = (cell) => cell.content !== undefined ? cell.content : cell.Content;

  const maxX = cells.length > 0 ? Math.max(...cells.map(c => getCellX(c)), 0) : 10;
  const maxY = cells.length > 0 ? Math.max(...cells.map(c => getCellY(c)), 0) : 10;
  
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

  // Function to determine cell color based on content
  const getCellColor = (content) => {
    switch (content) {
      case CellContent.Wall:
      case 1: // Numeric value for Wall
        return 'grey';
      case CellContent.Pellet:
      case 2: // Numeric value for Pellet
        return 'yellow';
      case CellContent.AnimalSpawn:
      case 3: // Numeric value for AnimalSpawn
        return 'lightblue';
      case CellContent.ZookeeperSpawn:
      case 4: // Numeric value for ZookeeperSpawn
        return 'lightcoral';
      case CellContent.Empty:
      case 0: // Numeric value for Empty
      default:
        return 'white';
    }
  };

  return (
    <div className="grid-container" ref={containerRef} style={{ 
      height: '100%', 
      width: '100%', 
      display: 'flex', 
      alignItems: 'flex-start', 
      justifyContent: 'flex-start',
      padding: '0',
      overflow: 'hidden'
    }}>
      <div className="grid" style={{ 
        position: 'relative', 
        width: gridWidth, 
        height: gridHeight, 
        border: '1px solid black', 
        backgroundColor: '#f0f0f0',
        margin: '0',
        boxSizing: 'border-box'
      }}>
        {/* Render Cells */}
        {cells.map((cell, index) => {
          const x = getCellX(cell);
          const y = getCellY(cell);
          const content = getCellContent(cell);
          
          if (x === undefined || y === undefined) {
            console.warn("Cell missing coordinates:", cell);
            return null;
          }
          
          return (
            <div
              key={`cell-${x}-${y}-${index}`}
              className={`cell cell-${content}`}
              style={{
                position: 'absolute', 
                left: x * tileSize, 
                top: y * tileSize,
                width: tileSize, 
                height: tileSize,
                backgroundColor: getCellColor(content),
                border: '1px solid #ddd', 
                boxSizing: 'border-box',
              }}
            />
          );
        })}
        
        {/* Render Animals */}
        {animals.map((animal, animalIndex) => {
          const animalId = getEntityId(animal);
          const animalX = getEntityX(animal);
          const animalY = getEntityY(animal);
          
          if (animalX === undefined || animalY === undefined) {
            console.warn("Animal missing coordinates:", animal);
            return null;
          }
          
          const animalNickname = getEntityNickname(animal);
          const fontSize = Math.max(Math.floor(tileSize / 2.5), 10);
          
          return (
            <React.Fragment key={`animal-group-${animalId || animalIndex}`}>
              <div // Animal circle
                title={`${animalNickname} (Animal)`}
                className="entity entity-animal"
                style={{
                  position: 'absolute',
                  left: animalX * tileSize + tileSize / 4,
                  top: animalY * tileSize + tileSize / 4,
                  width: tileSize / 2,
                  height: tileSize / 2,
                  backgroundColor: colorMap[animalId] || 'blue',
                  borderRadius: '50%',
                  zIndex: 2,
                  boxShadow: '0 0 2px black'
                }}
              />
              <div // Animal name label
                style={{
                  position: 'absolute',
                  left: animalX * tileSize + tileSize, // To the right of the animal
                  top: animalY * tileSize + tileSize / 4, // Align with top of animal circle
                  backgroundColor: 'rgba(144, 238, 144, 0.8)',
                  color: 'black',
                  padding: '1px 3px',
                  fontSize: `${fontSize}px`,
                  zIndex: 3,
                  whiteSpace: 'nowrap',
                  border: '1px solid #ccc',
                  borderRadius: '3px'
                }}
              >
                {animalNickname}
              </div>
            </React.Fragment>
          );
        })}
        
        {/* Render Zookeepers */}
        {zookeepers.map((zookeeper, zookeeperIndex) => {
          const zookeeperId = getEntityId(zookeeper);
          const zooX = getEntityX(zookeeper);
          const zooY = getEntityY(zookeeper);
          
          if (zooX === undefined || zooY === undefined) {
            console.warn("Zookeeper missing coordinates:", zookeeper);
            return null;
          }
          
          const zooNickname = getEntityNickname(zookeeper);
          const fontSize = Math.max(Math.floor(tileSize / 2.5), 10);
          
          return (
            <React.Fragment key={`zookeeper-group-${zookeeperId || zookeeperIndex}`}>
              <div // Zookeeper square
                title={`${zooNickname} (Zookeeper)`}
                className="entity entity-zookeeper"
                style={{
                  position: 'absolute',
                  left: zooX * tileSize + tileSize / 4,
                  top: zooY * tileSize + tileSize / 4,
                  width: tileSize / 2,
                  height: tileSize / 2,
                  backgroundColor: 'red',
                  borderRadius: '20%',
                  zIndex: 2,
                  boxShadow: '0 0 2px black'
                }}
              />
              <div // Zookeeper name label
                style={{
                  position: 'absolute',
                  left: zooX * tileSize + tileSize, // To the right
                  top: zooY * tileSize + tileSize / 4, // Align with top
                  backgroundColor: 'rgba(255, 204, 203, 0.8)',
                  color: 'black',
                  padding: '1px 3px',
                  fontSize: `${fontSize}px`,
                  zIndex: 3,
                  whiteSpace: 'nowrap',
                  border: '1px solid #ccc',
                  borderRadius: '3px'
                }}
              >
                {zooNickname}
              </div>
            </React.Fragment>
          );
        })}
      </div>
    </div>
  );
};

export default Grid;
