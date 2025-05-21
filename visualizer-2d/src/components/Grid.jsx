import React, { useEffect, useState, useRef } from 'react';
// Import from the new JavaScript models file
import { CellContent } from '../models.js'; 

/**
 * @param {object} props
 * @param {Array} props.cells - Array of cell objects
 * @param {Array} props.animals - Array of animal objects
 * @param {Array} props.zookeepers - Array of zookeeper objects
 * @param {object} props.colorMap - Map of animal IDs to colors
 * @param {boolean} props.showDetails - Whether to show additional details like positions and scores
 */
const Grid = ({ cells = [], animals = [], zookeepers = [], colorMap = {}, showDetails = false }) => {
  const containerRef = useRef(null);
  const [tileSize, setTileSize] = useState(20); // Default tile size, increased from 15
  const [containerSize, setContainerSize] = useState({ width: 0, height: 0 });
  const [hoveredEntity, setHoveredEntity] = useState(null); // Track which entity is being hovered

  // Debug logging to help diagnose grid content issues
  useEffect(() => {
    console.log("Grid data debug:");
    console.log("Cells:", cells.length, cells.slice(0, 5));
    console.log("Animals:", animals.length, animals);
    console.log("Zookeepers:", zookeepers.length, zookeepers);
    
    if (cells.length > 0) {
      const sampleCell = cells[0];
      console.log("Sample cell properties:", Object.keys(sampleCell));
      console.log("Sample cell X/Y:", sampleCell.x || sampleCell.X, sampleCell.y || sampleCell.Y);
      console.log("Sample cell content:", sampleCell.content || sampleCell.Content);
    }
    
    // Check for any potential content mapping issues
    const contentValues = new Set();
    cells.forEach(cell => {
      const content = cell.content !== undefined ? cell.content : cell.Content;
      contentValues.add(content);
    });
    console.log("Unique content values in cells:", Array.from(contentValues));
  }, [cells, animals, zookeepers]);

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
    // First, check if the entity has a nickname property (either case)
    if (entity.nickname !== undefined) return entity.nickname;
    if (entity.Nickname !== undefined) return entity.Nickname;
    
    // If no nickname found, use the ID as fallback
    const entityId = getEntityId(entity);
    return entityId ? `Bot-${entityId}` : 'Unknown Bot';
  };
  
  // Get entity score - handle both lowercase and uppercase property names
  const getEntityScore = (entity) => {
    return entity.score !== undefined ? entity.score : 
           entity.Score !== undefined ? entity.Score : 0;
  };
  
  // Get captured counter - handle both lowercase and uppercase property names
  const getEntityCaptured = (entity) => {
    return entity.capturedCounter !== undefined ? entity.capturedCounter : 
           entity.CapturedCounter !== undefined ? entity.CapturedCounter : 0;
  };
  
  // Get distance covered - handle both lowercase and uppercase property names
  const getEntityDistance = (entity) => {
    return entity.distanceCovered !== undefined ? entity.distanceCovered : 
           entity.DistanceCovered !== undefined ? entity.DistanceCovered : 0;
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
      
      // Calculate the maximum possible tile size that fits the grid completely
      const maxTileWidth = containerWidth / (maxX + 1);
      const maxTileHeight = containerHeight / (maxY + 1);
      
      // Use Math.floor to avoid overflow, cap at 40px maximum
      const newTileSize = Math.floor(Math.min(maxTileWidth, maxTileHeight, 40));
      
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
  
  // Add a helper function to convert hex color to RGBA
  const getColorWithOpacity = (color, opacity = 0.5) => {
    // If no color provided, return default
    if (!color) return 'rgba(144, 238, 144, 0.5)';
    
    // Check if color has a valid format
    console.log("Converting color to rgba:", color);
    
    // Return a fixed color based on the original (for debugging)
    switch (color) {
      case 'blue': return 'rgba(0, 0, 255, 0.5)';
      case 'green': return 'rgba(0, 128, 0, 0.5)';
      case 'purple': return 'rgba(128, 0, 128, 0.5)';
      case 'cyan': return 'rgba(0, 255, 255, 0.5)';
      case 'magenta': return 'rgba(255, 0, 255, 0.5)';
      case 'yellow': return 'rgba(255, 255, 0, 0.5)';
      case 'lime': return 'rgba(0, 255, 0, 0.5)';
      case 'teal': return 'rgba(0, 128, 128, 0.5)';
      default: return 'rgba(144, 238, 144, 0.5)';
    }
  };

  return (
    <div className="grid-container" id="grid-main-container" ref={containerRef} style={{ height: '100%', width: '100%' }}>
      <div className="grid-layout" id="grid-cells-layout" style={{ 
        width: gridWidth, 
        height: gridHeight 
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
              id={`grid-cell-${x}-${y}`}
              className={`grid-cell cell-type-${content}`}
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
          
          // Get additional stats when showDetails is true
          const animalScore = showDetails ? getEntityScore(animal) : null;
          const animalCaptured = showDetails ? getEntityCaptured(animal) : null;
          const animalDistance = showDetails ? getEntityDistance(animal) : null;
          
          // Check if this animal is being hovered
          const isHovered = hoveredEntity === `animal-${animalId || animalIndex}`;
          
          // Prepare the detail text
          let detailText = animalNickname;
          if (showDetails && isHovered) {
            // Only show detailed info when hovered and showDetails is true
            detailText = `${animalNickname} (${animalX},${animalY})`;
            if (animalScore !== null) {
              detailText += ` S:${animalScore}`;
            }
            if (animalCaptured !== null) {
              detailText += ` C:${animalCaptured}`;
            }
            if (animalDistance !== null) {
              detailText += ` D:${animalDistance}`;
            }
          }
          
          // Enhanced tooltip text for hover
          const tooltipText = showDetails 
            ? `${animalNickname} - Position: (${animalX},${animalY}) - Score: ${animalScore} - Captured: ${animalCaptured} - Distance: ${animalDistance}`
            : `${animalNickname} (Animal)`;
          
          // Get animal color
          const animalColor = colorMap[animalId];
          
          return (
            <React.Fragment key={`animal-group-${animalId || animalIndex}`}>
              <div // Animal circle
                id={`animal-circle-${animalId || animalIndex}`}
                className="entity-marker animal-marker"
                title={tooltipText}
                style={{
                  position: 'absolute',
                  left: animalX * tileSize + tileSize / 4,
                  top: animalY * tileSize + tileSize / 4,
                  width: tileSize / 2,
                  height: tileSize / 2,
                  backgroundColor: animalColor || 'blue',
                  borderRadius: '50%',
                  zIndex: 2,
                  boxShadow: '0 0 2px black'
                }}
              />
              <div // Animal name label
                id={`animal-label-${animalId || animalIndex}`}
                className="entity-label animal-label"
                onMouseEnter={() => setHoveredEntity(`animal-${animalId || animalIndex}`)}
                onMouseLeave={() => setHoveredEntity(null)}
                style={{
                  position: 'absolute',
                  left: animalX * tileSize + tileSize, // To the right of the animal
                  top: animalY * tileSize + tileSize / 4 - 1, // Center with the circle (subtracting half of padding)
                  backgroundColor: getColorWithOpacity(animalColor, 0.5),
                  color: 'black',
                  padding: '1px 3px',
                  fontSize: `${fontSize}px`,
                  fontWeight: 'bold',
                  textShadow: '0px 0px 1px white',
                  zIndex: 3,
                  whiteSpace: 'nowrap',
                  border: '1px solid #ccc',
                  borderRadius: '3px',
                  maxWidth: showDetails ? '200px' : 'auto', // Limit width when showing details
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                  transition: 'background-color 0.2s ease, opacity 0.2s ease',
                  opacity: isHovered ? 1 : 0.9,
                  display: 'flex',
                  alignItems: 'center',
                  height: `${tileSize / 2}px` // Match height of the circle
                }}
              >
                {detailText}
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
          
          // Check if this zookeeper is being hovered
          const isHovered = hoveredEntity === `zookeeper-${zookeeperId || zookeeperIndex}`;
          
          // Enhanced tooltip and detail text for zookeepers in replay mode
          const tooltipText = showDetails 
            ? `${zooNickname} (Zookeeper) - Position: (${zooX},${zooY})`
            : `${zooNickname} (Zookeeper)`;
            
          const detailText = showDetails && isHovered
            ? `${zooNickname} (${zooX},${zooY})`
            : zooNickname;
          
          return (
            <React.Fragment key={`zookeeper-group-${zookeeperId || zookeeperIndex}`}>
              <div // Zookeeper square
                id={`zookeeper-square-${zookeeperId || zookeeperIndex}`}
                className="entity-marker zookeeper-marker"
                title={tooltipText}
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
                id={`zookeeper-label-${zookeeperId || zookeeperIndex}`}
                className="entity-label zookeeper-label"
                onMouseEnter={() => setHoveredEntity(`zookeeper-${zookeeperId || zookeeperIndex}`)}
                onMouseLeave={() => setHoveredEntity(null)}
                style={{
                  position: 'absolute',
                  left: zooX * tileSize + tileSize, // To the right
                  top: zooY * tileSize + tileSize / 4 - 1, // Center with the square (subtracting half of padding)
                  backgroundColor: 'rgba(255, 160, 160, 0.4)',
                  color: 'black',
                  padding: '1px 3px',
                  fontSize: `${fontSize}px`,
                  fontWeight: 'bold',
                  textShadow: '0px 0px 1px white',
                  zIndex: 3,
                  whiteSpace: 'nowrap',
                  border: '1px solid #ccc',
                  borderRadius: '3px',
                  transition: 'background-color 0.2s ease, opacity 0.2s ease',
                  opacity: isHovered ? 1 : 0.85,
                  display: 'flex',
                  alignItems: 'center',
                  height: `${tileSize / 2}px` // Match height of the square
                }}
              >
                {detailText}
              </div>
            </React.Fragment>
          );
        })}
      </div>
    </div>
  );
};

export default Grid;
