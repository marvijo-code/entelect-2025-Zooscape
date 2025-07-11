import React, { useEffect, useState, useRef, useMemo, useCallback, memo } from 'react';
import './Grid.css'; // Import CSS for the Grid component
// Import from the new JavaScript models file
import { CellContent } from '../models.js';
import '../setupLogging.js';

// Performance optimization constants - UPDATED for GPU relief
const RENDER_OPTIMIZATION = {
  USE_TRANSFORM: false,  // DISABLED: CSS transforms cause GPU overload
  WILL_CHANGE: false,    // DISABLED: will-change creates unnecessary GPU layers
  CONTAIN: true,         // Keep CSS containment for better rendering isolation
  BATCH_SIZE: 50,        // REDUCED: Smaller batches to prevent blocking
  USE_VIRTUALIZATION: true, // Enable cell virtualization for large grids
  MAX_RENDERED_CELLS: 2000  // Limit rendered cells for performance
};

/**
 * @param {object} props
 * @param {Array} props.cells - Array of cell objects
 * @param {Array} props.animals - Array of animal objects
 * @param {Array} props.zookeepers - Array of zookeeper objects
 * @param {object} props.colorMap - Map of animal IDs to colors
 * @param {boolean} props.showDetails - Whether to show additional details like positions and scores
 * @param {object} props.leaderBoard - Per-tick leaderboard data
 */
const Grid = memo(({ cells = [], animals = [], zookeepers = [], colorMap = {}, showDetails = false, leaderBoard = {} }) => {
  // Early return BEFORE any hooks to avoid Rules of Hooks violation
  if (!cells || cells.length === 0) {
    console.log("Grid component: No cells provided, showing loading message");
    return (
      <div className="grid-loading">
        <p>Waiting for grid data...</p>
        <p className="debug-info">No cell data available. Check server connection or game state.</p>
      </div>
    );
  }

  console.log(`Grid component received: ${cells.length} cells, ${animals.length} animals, ${zookeepers.length} zookeepers`);
  console.log("Grid component sample cells:", cells.slice(0, 3));

  const containerRef = useRef(null);
  const [tileSize, setTileSize] = useState(20); // Default tile size, increased from 15
  const [containerSize, setContainerSize] = useState({ width: 0, height: 0 });
  const [hoverInfo, setHoverInfo] = useState(null); // REPLACED: hoveredEntity with hoverInfo (object)
  const [tooltipStyle, setTooltipStyle] = useState({ display: 'none' });

  // Memoize coordinate getters to avoid recreating functions on every render
  const getCellX = useCallback((cell) => cell.x !== undefined ? cell.x : cell.X, []);
  const getCellY = useCallback((cell) => cell.y !== undefined ? cell.y : cell.Y, []);
  const getEntityX = useCallback((entity) => entity.x !== undefined ? entity.x : entity.X, []);
  const getEntityY = useCallback((entity) => entity.y !== undefined ? entity.y : entity.Y, []);
  const getEntityId = useCallback((entity) => entity.id !== undefined ? entity.id : entity.Id, []);
  const getCellContent = useCallback((cell) => cell.content !== undefined ? cell.content : cell.Content, []);

  // Memoize entity property getters
  const getEntityNickname = useCallback((entity) => {
    if (entity.nickname !== undefined) return entity.nickname;
    if (entity.Nickname !== undefined) return entity.Nickname;
    const entityId = getEntityId(entity);
    return entityId ? `Bot-${entityId}` : 'Unknown Bot';
  }, [getEntityId]);

  const getEntityScore = useCallback((entity) => {
    return entity.score !== undefined ? entity.score :
      entity.Score !== undefined ? entity.Score : 0;
  }, []);

  const getEntityCaptured = useCallback((entity) => {
    return entity.capturedCounter !== undefined ? entity.capturedCounter :
      entity.CapturedCounter !== undefined ? entity.CapturedCounter : 0;
  }, []);

  const getEntityDistance = useCallback((entity) => {
    return entity.distanceCovered !== undefined ? entity.distanceCovered :
      entity.DistanceCovered !== undefined ? entity.DistanceCovered : 0;
  }, []);

  const getEntityHeldPowerUp = useCallback((entity) => entity.heldPowerUp !== undefined ? entity.heldPowerUp : entity.HeldPowerUp, []);
  const getEntityActivePowerUp = useCallback((entity) => entity.activePowerUp !== undefined ? entity.activePowerUp : entity.ActivePowerUp, []);
  const getEntityPowerUpDuration = useCallback((entity) => entity.powerUpDuration !== undefined ? entity.powerUpDuration : entity.PowerUpDuration, []);
  const getEntityScoreStreak = useCallback((entity) => entity.scoreStreak !== undefined ? entity.scoreStreak : entity.ScoreStreak, []);

  const getPowerUpName = useCallback((powerUpContent) => {
    if (powerUpContent === null || powerUpContent === undefined) return 'None';
    const entry = Object.entries(CellContent).find(([key, value]) => value === powerUpContent);
    return entry ? entry[0] : 'Unknown';
  }, []);

  // Memoize grid dimensions calculation
  const { maxX, maxY } = useMemo(() => {
    if (cells.length === 0) return { maxX: 10, maxY: 10 };

    let maxX = 0, maxY = 0;
    for (const cell of cells) {
      const x = getCellX(cell);
      const y = getCellY(cell);
      if (x > maxX) maxX = x;
      if (y > maxY) maxY = y;
    }
    return { maxX, maxY };
  }, [cells, getCellX, getCellY]);

  // Debug logging to help diagnose grid content issues - only log when data changes
  // Reduced logging frequency to improve performance
  useEffect(() => {
    if (cells.length > 0 && cells.length % 10 === 0) { // Only log every 10th update
      console.log("Grid data debug:", {
        cellsLength: cells.length,
        animalsLength: animals.length,
        zookeepersLength: zookeepers.length,
        maxX,
        maxY,
        tileSize
      });
    }
  }, [cells.length, animals.length, zookeepers.length, maxX, maxY, tileSize]);

  // Calculate the optimal tile size based on container dimensions - optimized with debounce
  useEffect(() => {
    if (!containerRef.current) return;

    let resizeTimeout;

    const updateSize = () => {
      if (!containerRef.current) return; // Add null check

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

      const proposedSize = Math.max(newTileSize, 12); // cap min
      if (proposedSize !== tileSize) {
        setTileSize(proposedSize);
      }
    };

    // Debounced resize handler
    const debouncedResize = () => {
      clearTimeout(resizeTimeout);
      resizeTimeout = setTimeout(updateSize, 250);
    };

    // Initial size calculation
    updateSize();

    // Add resize observer to handle container size changes
    const resizeObserver = new ResizeObserver(debouncedResize);
    resizeObserver.observe(containerRef.current);

    // Cleanup
    return () => {
      clearTimeout(resizeTimeout);
      if (containerRef.current) {
        resizeObserver.unobserve(containerRef.current);
      }
      resizeObserver.disconnect();
    };
  }, [maxX, maxY]);

  const gridWidth = (maxX + 1) * tileSize;
  const gridHeight = (maxY + 1) * tileSize;

  console.log(`Grid dimensions: ${gridWidth}x${gridHeight} (maxX=${maxX}, maxY=${maxY}, tileSize=${tileSize})`);
  console.log(`Container size: ${containerSize.width}x${containerSize.height}`);

  // Debug: Check if we have cells across different X coordinates
  const uniqueXCoords = new Set();
  const uniqueYCoords = new Set();
  cells.slice(0, 100).forEach(cell => {
    uniqueXCoords.add(getCellX(cell));
    uniqueYCoords.add(getCellY(cell));
  });
  console.log(`Unique X coordinates (first 100 cells): [${Array.from(uniqueXCoords).sort((a, b) => a - b).slice(0, 10).join(', ')}...]`);
  console.log(`Unique Y coordinates (first 100 cells): [${Array.from(uniqueYCoords).sort((a, b) => a - b).slice(0, 10).join(', ')}...]`);

  // Memoize cell color function
  const getCellColor = useCallback((content) => {
    switch (content) {
      case CellContent.Wall:
      case 1: // Numeric value for Wall
        return '#404040'; // Darker grey for better visibility
      case CellContent.Pellet:
      case 2: // Numeric value for Pellet
        return '#FFD700'; // Gold color for pellets
      case CellContent.AnimalSpawn:
      case 3: // Numeric value for AnimalSpawn
        return '#87CEEB'; // Sky blue
      case CellContent.ZookeeperSpawn:
      case 4: // Numeric value for ZookeeperSpawn
        return '#F08080'; // Light coral
      case CellContent.PowerPellet:
      case 5:
        return '#FF69B4'; // Hot Pink for PowerPellet
      case CellContent.ChameleonCloak:
      case 6:
        return '#9370DB'; // Medium Purple for ChameleonCloak
      case CellContent.Scavenger:
      case 7:
        return '#FFA500'; // Orange for Scavenger
      case CellContent.BigMooseJuice:
      case 8:
        return '#DC143C'; // Crimson for BigMooseJuice
      case CellContent.Empty:
      case 0: // Numeric value for Empty
      default:
        return '#F5F5F5'; // Light grey for empty cells
    }
  }, []);

  // Memoize color conversion function
  const getColorWithOpacity = useCallback((color, opacity = 0.5) => {
    if (!color) return 'rgba(144, 238, 144, 0.5)';

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
  }, []);

  // Memoize rendered cells
  const renderedCells = useMemo(() => {
    return cells.map((cell, index) => { // Simplified map, index is from the original cells array
      const x = getCellX(cell);
      const y = getCellY(cell);
      const content = getCellContent(cell);
      const color = getCellColor(content);
      const cellBorder = '1px solid #333';
      const zIndex = 1; // Default z-index for cells, above background

      if (x === undefined || y === undefined) return null;

      const positionStyle = RENDER_OPTIMIZATION.USE_TRANSFORM ?
        { transform: `translate(${x * tileSize}px, ${y * tileSize}px)` } :
        { left: x * tileSize, top: y * tileSize };

      return (
        <div
          key={`cell-${x}-${y}-${index}`}
          id={`grid-cell-${x}-${y}`}
          className={`dynamic-grid-cell cell-content-${content}`}
          onMouseEnter={() => setHoverInfo({ type: 'cell', x, y })} // MODIFIED
          onMouseLeave={() => setHoverInfo(null)}                   // MODIFIED
          style={{
            position: 'absolute',
            ...positionStyle,
            width: tileSize,
            height: tileSize,
            backgroundColor: color,
            border: cellBorder,
            boxSizing: 'border-box',
            zIndex: zIndex,
            opacity: 1, // Ensure cells are opaque
          }}
        />
      );
    }).filter(Boolean);
  }, [cells, tileSize, getCellX, getCellY, getCellContent, getCellColor /*, setHoverInfo (added if required by linter) */]);

  // Memoize rendered animals
  const renderedAnimals = useMemo(() => {
    return animals.map((animal, animalIndex) => {
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
      const heldPowerUp = showDetails ? getEntityHeldPowerUp(animal) : null;
      const activePowerUp = showDetails ? getEntityActivePowerUp(animal) : null;
      const powerUpDuration = showDetails ? getEntityPowerUpDuration(animal) : null;
      const scoreStreak = showDetails ? getEntityScoreStreak(animal) : null;

      // Check if this animal is being hovered
      const isHovered = hoverInfo && hoverInfo.type === 'animal' && (hoverInfo.id === animalId || hoverInfo.index === animalIndex);

      // Prepare the detail text
      let detailText = animalNickname;
      if (showDetails && isHovered) {
        detailText = `${animalNickname} (${animalX},${animalY})`;
        if (animalScore !== null) detailText += ` S:${animalScore}`;
        if (scoreStreak > 0) detailText += ` Streak:${scoreStreak}`;
        if (heldPowerUp) detailText += ` Held:${getPowerUpName(heldPowerUp)}`;
        if (activePowerUp) detailText += ` Active:${getPowerUpName(activePowerUp)} (${powerUpDuration})`;
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
            onMouseEnter={() => setHoverInfo({ type: 'animal', id: animalId, index: animalIndex, x: animalX, y: animalY, entity: animal })} // MODIFIED
            onMouseLeave={() => setHoverInfo(null)} // MODIFIED
            style={{
              position: 'absolute',
              ...(RENDER_OPTIMIZATION.USE_TRANSFORM
                ? { transform: `translate(${animalX * tileSize + tileSize / 4}px, ${animalY * tileSize + tileSize / 4}px)` }
                : { left: animalX * tileSize + tileSize / 4, top: animalY * tileSize + tileSize / 4 }),
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
            style={{
              position: 'absolute',
              left: animalX * tileSize + tileSize,
              top: animalY * tileSize + tileSize / 4 - 2,
              backgroundColor: 'rgba(255, 255, 255, 0.95)', // High contrast white background
              color: '#000000',
              padding: '3px 6px',
              fontSize: `${Math.max(Math.floor(tileSize / 1.8), 12)}px`, // Larger font size
              fontWeight: 'bold',
              textShadow: '1px 1px 2px rgba(0,0,0,0.3), -1px -1px 2px rgba(0,0,0,0.3)', // Enhanced text shadow
              zIndex: 4, // Higher z-index to ensure visibility
              whiteSpace: 'nowrap',
              border: `2px solid ${animalColor || 'blue'}`, // Colored border matching animal
              borderRadius: '4px',
              maxWidth: showDetails ? '300px' : 'auto',
              overflow: 'visible',
              textOverflow: 'clip',
              transition: 'all 0.2s ease',
              opacity: 1, // Always fully opaque
              display: 'flex',
              alignItems: 'center',
              height: `${Math.max(tileSize / 2, 18)}px`, // Minimum height for readability
              boxShadow: '0 2px 4px rgba(0,0,0,0.2)', // Drop shadow for depth
              transform: isHovered ? 'scale(1.05)' : 'scale(1)', // Slight scale on hover
            }}
          >
            {detailText}
          </div>
        </React.Fragment>
      );
    });
  }, [animals, tileSize, colorMap, showDetails, hoverInfo, getEntityId, getEntityX, getEntityY, getEntityNickname, getEntityScore, getEntityCaptured, getEntityDistance, getColorWithOpacity /*, setHoverInfo */]);

  // Memoize rendered zookeepers
  const renderedZookeepers = useMemo(() => {
    return zookeepers.map((zookeeper, zookeeperIndex) => {
      const zookeeperId = getEntityId(zookeeper);
      const zooX = getEntityX(zookeeper);
      const zooY = getEntityY(zookeeper);

      if (zooX === undefined || zooY === undefined) {
        console.warn("Zookeeper missing coordinates:", zookeeper);
        return null;
      }

      const zooNickname = getEntityNickname(zookeeper);
      const fontSize = Math.max(Math.floor(tileSize / 2.2), 11);

      // Check if this zookeeper is being hovered
      const isHovered = hoverInfo && hoverInfo.type === 'zookeeper' && (hoverInfo.id === zookeeperId || hoverInfo.index === zookeeperIndex);

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
            onMouseEnter={() => setHoverInfo({ type: 'zookeeper', id: zookeeperId, index: zookeeperIndex, x: zooX, y: zooY, entity: zookeeper })} // MODIFIED
            onMouseLeave={() => setHoverInfo(null)} // MODIFIED
            style={{
              position: 'absolute',
              ...(RENDER_OPTIMIZATION.USE_TRANSFORM
                ? { transform: `translate(${zooX * tileSize + tileSize / 4}px, ${zooY * tileSize + tileSize / 4}px)` }
                : { left: zooX * tileSize + tileSize / 4, top: zooY * tileSize + tileSize / 4 }),
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
            style={{
              position: 'absolute',
              left: zooX * tileSize + tileSize,
              top: zooY * tileSize + tileSize / 4 - 2,
              backgroundColor: 'rgba(255, 255, 255, 0.95)', // High contrast white background
              color: '#000000',
              padding: '3px 6px',
              fontSize: `${Math.max(Math.floor(tileSize / 1.8), 12)}px`, // Larger font size matching animals
              fontWeight: 'bold',
              textShadow: '1px 1px 2px rgba(0,0,0,0.3), -1px -1px 2px rgba(0,0,0,0.3)', // Enhanced text shadow
              zIndex: 4, // Higher z-index to ensure visibility
              whiteSpace: 'nowrap',
              border: '2px solid red', // Red border matching zookeeper color
              borderRadius: '4px',
              transition: 'all 0.2s ease',
              opacity: 1, // Always fully opaque
              display: 'flex',
              alignItems: 'center',
              height: `${Math.max(tileSize / 2, 18)}px`, // Minimum height for readability
              boxShadow: '0 2px 4px rgba(0,0,0,0.2)', // Drop shadow for depth
              transform: isHovered ? 'scale(1.05)' : 'scale(1)', // Slight scale on hover
            }}
          >
            {detailText}
          </div>
        </React.Fragment>
      );
    });
  }, [zookeepers, tileSize, showDetails, hoverInfo, getEntityId, getEntityX, getEntityY, getEntityNickname /*, setHoverInfo */]);

  // Memoize rendered per-tick scoreboard
  const renderedPerTickScoreboard = useMemo(() => {
    // Scoreboard moved to App.jsx right panel - no longer needed here
    return null;
  }, []);

  // Effect to calculate tooltip style and content
  useEffect(() => {
    if (!hoverInfo) {
      setTooltipStyle({ display: 'none' });
      return;
    }

    const gridActualWidth = (maxX + 1) * tileSize; // Use actual grid dimensions
    let newTop = 0;
    let newLeft = 0;
    let content = '';
    let nearEdge = false;
    let tooltipEstimatedWidth = 180; // Default estimate for edge detection
    const offset = 5; // Small gap from the element

    if (hoverInfo.type === 'cell') {
      newTop = hoverInfo.y * tileSize;
      newLeft = (hoverInfo.x + 1) * tileSize + offset;
      content = `(${hoverInfo.x}, ${hoverInfo.y})`;
      tooltipEstimatedWidth = 80; // Narrower estimate for cell coordinates
      if (newLeft + tooltipEstimatedWidth > gridActualWidth) {
        nearEdge = true;
        newLeft = hoverInfo.x * tileSize - tooltipEstimatedWidth - offset; // Position to the left
      }
    } else if (hoverInfo.type === 'animal' || hoverInfo.type === 'zookeeper') {
      const entity = hoverInfo.entity;
      const entityX = hoverInfo.x;
      const entityY = hoverInfo.y;
      const nickname = getEntityNickname(entity);

      newTop = entityY * tileSize; // Position relative to the entity marker
      newLeft = (entityX + 1) * tileSize + offset; // Default to the right

      content = `${nickname} (${hoverInfo.type})`;
      content += ` - Pos: (${entityX},${entityY})`;

      if (hoverInfo.type === 'animal' && showDetails) {
        content += ` - Scr: ${getEntityScore(entity)} - Cap: ${getEntityCaptured(entity)} - Dis: ${getEntityDistance(entity)}`;
      }

      if (newLeft + tooltipEstimatedWidth > gridActualWidth) {
        nearEdge = true;
        newLeft = entityX * tileSize - tooltipEstimatedWidth - offset;
      }
    }

    setTooltipStyle({
      display: 'block',
      top: `${newTop}px`,
      left: nearEdge && newLeft < 0 ? `${offset}px` : `${newLeft}px`, // Ensure not off-screen left
      right: nearEdge && newLeft >= 0 ? 'auto' : (nearEdge ? `${offset}px` : 'auto'), // Handle right positioning if nearEdge and newLeft is positive
      /* REMOVED: transform: 'translateY(0)'; - GPU intensive transforms */
      // zIndex should be handled by CSS class .entity-tooltip
    });
    // Store content in a separate state or directly use it if tooltip component can take content as prop
    // For simplicity, let's assume `tooltipContent` state will be set here or used by the tooltip
  }, [hoverInfo, tileSize, maxX, animals, zookeepers, showDetails, getEntityNickname, getEntityScore, getEntityCaptured, getEntityDistance]);

  // Prepare tooltipContent for rendering
  let currentTooltipContent = '';
  if (hoverInfo) {
    if (hoverInfo.type === 'cell') {
      currentTooltipContent = `(${hoverInfo.x}, ${hoverInfo.y})`;
    } else if (hoverInfo.type === 'animal' || hoverInfo.type === 'zookeeper') {
      const entity = hoverInfo.entity;
      const nickname = getEntityNickname(entity);
      currentTooltipContent = `${nickname} (${hoverInfo.type}) - Pos: (${hoverInfo.x},${hoverInfo.y})`;
      if (hoverInfo.type === 'animal' && showDetails) {
        currentTooltipContent += ` - Scr: ${getEntityScore(entity)} - Cap: ${getEntityCaptured(entity)} - Dis: ${getEntityDistance(entity)}`;
      }
    }
  }

  return (
    <div className="grid-container" ref={containerRef}>
      <div
        className="grid"
        style={{
          position: 'relative',
          width: `${gridWidth}px`,
          height: `${gridHeight}px`,
          willChange: RENDER_OPTIMIZATION.WILL_CHANGE ? 'transform' : 'auto',
          contain: RENDER_OPTIMIZATION.CONTAIN ? 'layout style paint' : 'none',
          /* REMOVED: transform: 'translateZ(0)'; - Causes GPU overload */
        }}
      >
        {renderedCells}
        {renderedAnimals}
        {renderedZookeepers}
        {renderedPerTickScoreboard}

        {/* Hover tooltip - controlled by hoverInfo and tooltipStyle */}
        {hoverInfo && currentTooltipContent && (
          <div
            className={`entity-tooltip ${hoverInfo && hoverInfo.type === 'cell' ? 'cell-coordinate-tooltip' : ''}`}
            style={tooltipStyle}
          >
            {currentTooltipContent}
          </div>
        )}
      </div>
    </div>
  );
});

Grid.displayName = 'Grid';

export default Grid;
