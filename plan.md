# Zooscape 2D Visualizer Performance Optimization Plan

## Executive Summary

The visualizer is experiencing severe performance issues with 100% GPU usage in Chrome. Based on code analysis, the primary issues are:

1. **Excessive CSS animations and transforms** causing GPU overload
2. **Inefficient DOM rendering** with too many elements
3. **Continuous re-renders** without proper optimization
4. **Memory leaks** from unoptimized React patterns
5. **CSS backdrop filters and effects** overwhelming the compositor

## Critical Issues Identified

### 1. GPU Overload from CSS Effects
**Severity: CRITICAL**

**Problems:**
- Multiple `backdrop-filter: blur()` effects on many elements
- Continuous `labelPulse` animation on ALL entity labels (3s infinite)
- Scale transforms on hover for every grid element
- `will-change` properties overused
- Multiple box-shadows with blur effects

**Evidence from code:**
```css
/* Grid.css - Line 174-197 */
.entity-label {
  animation: labelPulse 3s ease-in-out infinite; /* EVERY label animating */
}

.animal-label, .zookeeper-label {
  backdrop-filter: blur(2px); /* GPU intensive */
  -webkit-backdrop-filter: blur(2px);
}

.entity-label:hover {
  transform: scale(1.1) !important; /* Triggers recomposition */
}
```

### 2. Excessive DOM Elements
**Severity: HIGH**

**Problems:**
- Grid renders potentially thousands of cells simultaneously
- Each animal/zookeeper has 2 DOM elements (circle + label)
- No virtualization for large grids
- Every cell has hover effects and tooltips

**Evidence:**
```jsx
// Grid.jsx - Lines 235-280
const renderedCells = useMemo(() => {
  return cells.map((cell, index) => { // Could be 10,000+ cells
    // Each cell creates a DOM element with hover handlers
  });
}, [cells, tileSize, /* many deps */]);
```

### 3. React Performance Anti-patterns
**Severity: HIGH**

**Problems:**
- Heavy computations in render without proper memoization
- Inline style objects recreated on every render
- Too many useEffect dependencies causing cascading re-renders
- State updates in rapid succession

**Evidence:**
```jsx
// App.jsx - Lines 646-700
useEffect(() => {
  // Complex processing logic that could block main thread
  const processLiveTickInternal = () => {
    // Heavy processing without yielding to browser
  };
}, [liveTickQueue, showReplayMode, playbackSpeed, /* many deps */]);
```

## Optimization Plan

### Phase 1: Immediate GPU Relief (Priority 1 - 1-2 days)

#### 1.1 Remove/Reduce Animations
```css
/* REMOVE these expensive animations */
.entity-label {
  /* animation: labelPulse 3s ease-in-out infinite; */ /* REMOVE */
}

/* REPLACE hover transforms with simpler effects */
.entity-label:hover {
  /* transform: scale(1.1) !important; */ /* REMOVE */
  opacity: 0.8; /* Use opacity instead */
  background-color: rgba(255, 255, 255, 1); /* Simple color change */
}
```

#### 1.2 Eliminate Backdrop Filters
```css
/* REMOVE backdrop filters */
.animal-label, .zookeeper-label {
  /* backdrop-filter: blur(2px); */ /* REMOVE */
  /* -webkit-backdrop-filter: blur(2px); */ /* REMOVE */
  background-color: rgba(255, 255, 255, 0.95); /* Use solid background */
}
```

#### 1.3 Optimize Will-Change Usage
```css
/* Only use will-change on elements that actually change */
.grid-container {
  /* will-change: transform; */ /* REMOVE unless actively transforming */
}

/* Add will-change only during interactions */
.entity-label:hover {
  will-change: opacity;
}
.entity-label {
  will-change: auto; /* Reset after interaction */
}
```

### Phase 2: DOM and Rendering Optimization (Priority 2 - 2-3 days)

#### 2.1 Implement Grid Virtualization
```jsx
// Create VirtualGrid component
import { FixedSizeGrid as Grid } from 'react-window';

const VirtualGrid = ({ cells, animals, zookeepers, tileSize }) => {
  const Cell = ({ columnIndex, rowIndex, style }) => {
    // Only render cells in viewport
    const cellData = getCellAt(columnIndex, rowIndex);
    return (
      <div style={style}>
        {/* Render only visible cell */}
      </div>
    );
  };

  return (
    <Grid
      columnCount={maxX + 1}
      rowCount={maxY + 1}
      columnWidth={tileSize}
      rowHeight={tileSize}
      height={containerHeight}
      width={containerWidth}
    >
      {Cell}
    </Grid>
  );
};
```

#### 2.2 Reduce Entity Label Complexity
```jsx
// Simplify entity rendering
const EntityLabel = React.memo(({ entity, tileSize, isHovered }) => {
  const style = useMemo(() => ({
    position: 'absolute',
    left: entity.x * tileSize + tileSize,
    top: entity.y * tileSize + tileSize / 4,
    backgroundColor: 'rgba(255, 255, 255, 0.9)',
    padding: '2px 4px',
    fontSize: `${Math.max(tileSize / 2, 10)}px`,
    // Remove expensive properties
  }), [entity.x, entity.y, tileSize]);

  return (
    <div style={style}>
      {entity.nickname}
    </div>
  );
});
```

#### 2.3 Optimize Hover Handling
```jsx
// Use single hover handler for entire grid
const GridContainer = () => {
  const [hoveredCell, setHoveredCell] = useState(null);
  
  const handleMouseMove = useCallback((e) => {
    const rect = e.currentTarget.getBoundingClientRect();
    const x = Math.floor((e.clientX - rect.left) / tileSize);
    const y = Math.floor((e.clientY - rect.top) / tileSize);
    setHoveredCell({ x, y });
  }, [tileSize]);

  return (
    <div onMouseMove={handleMouseMove}>
      {/* Single hover handler instead of per-element */}
    </div>
  );
};
```

### Phase 3: React Performance Optimization (Priority 3 - 3-4 days)

#### 3.1 Memoization Strategy
```jsx
// Aggressive memoization for expensive computations
const GridData = React.memo(({ currentGameState }) => {
  const processedData = useMemo(() => {
    // Move heavy processing outside render
    return processGameState(currentGameState);
  }, [currentGameState.tick]); // Only recompute when tick changes

  return processedData;
});

// Memoize style objects
const useEntityStyles = (entity, tileSize) => {
  return useMemo(() => ({
    circle: {
      position: 'absolute',
      left: entity.x * tileSize + tileSize / 4,
      top: entity.y * tileSize + tileSize / 4,
      width: tileSize / 2,
      height: tileSize / 2,
    },
    label: {
      position: 'absolute',
      left: entity.x * tileSize + tileSize,
      top: entity.y * tileSize + tileSize / 4,
    }
  }), [entity.x, entity.y, tileSize]);
};
```

#### 3.2 Reduce Re-render Frequency
```jsx
// Debounce rapid state updates
const useDebouncedState = (initialValue, delay) => {
  const [value, setValue] = useState(initialValue);
  const [debouncedValue, setDebouncedValue] = useState(initialValue);

  useEffect(() => {
    const handler = setTimeout(() => {
      setDebouncedValue(value);
    }, delay);

    return () => clearTimeout(handler);
  }, [value, delay]);

  return [debouncedValue, setValue];
};

// Use for frequently updating state
const [gameState, setGameState] = useDebouncedState(null, 100);
```

#### 3.3 Optimize useEffect Dependencies
```jsx
// Split complex effects into smaller, focused ones
useEffect(() => {
  // Only handle tick processing
  processLiveTick();
}, [liveTickQueue.length]); // Minimal dependencies

useEffect(() => {
  // Only handle color mapping
  updateAnimalColors();
}, [animals]); // Separate concern

// Use refs for values that don't need to trigger re-renders
const playbackSpeedRef = useRef(playbackSpeed);
playbackSpeedRef.current = playbackSpeed;
```

### Phase 4: Advanced Optimizations (Priority 4 - 1-2 weeks)

#### 4.1 Web Workers for Heavy Processing
```jsx
// Move game state processing to Web Worker
const gameStateWorker = new Worker('/gameStateProcessor.js');

const processGameStateInWorker = (gameState) => {
  return new Promise((resolve) => {
    gameStateWorker.postMessage(gameState);
    gameStateWorker.onmessage = (e) => resolve(e.data);
  });
};
```

#### 4.2 Canvas Rendering for Grid
```jsx
// Replace DOM grid with Canvas for better performance
const CanvasGrid = ({ cells, animals, zookeepers }) => {
  const canvasRef = useRef(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    const ctx = canvas.getContext('2d');
    
    // Clear and redraw
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    
    // Draw cells
    cells.forEach(cell => {
      ctx.fillStyle = getCellColor(cell.content);
      ctx.fillRect(cell.x * tileSize, cell.y * tileSize, tileSize, tileSize);
    });
    
    // Draw entities
    animals.forEach(animal => {
      ctx.fillStyle = animal.color;
      ctx.beginPath();
      ctx.arc(
        animal.x * tileSize + tileSize/2,
        animal.y * tileSize + tileSize/2,
        tileSize/4,
        0,
        2 * Math.PI
      );
      ctx.fill();
    });
  }, [cells, animals, zookeepers, tileSize]);

  return <canvas ref={canvasRef} />;
};
```

#### 4.3 Implement Intersection Observer
```jsx
// Only render elements in viewport
const useIntersectionObserver = (ref, options) => {
  const [isIntersecting, setIsIntersecting] = useState(false);

  useEffect(() => {
    const observer = new IntersectionObserver(
      ([entry]) => setIsIntersecting(entry.isIntersecting),
      options
    );

    if (ref.current) observer.observe(ref.current);

    return () => observer.disconnect();
  }, [ref, options]);

  return isIntersecting;
};
```

## Implementation Timeline

### Week 1: Critical GPU Fixes
- **Day 1**: Remove all animations and backdrop filters
- **Day 2**: Optimize CSS transforms and will-change usage
- **Day 3**: Test and measure GPU usage improvement

### Week 2: DOM Optimization
- **Day 1-2**: Implement grid virtualization
- **Day 3-4**: Simplify entity rendering
- **Day 5**: Optimize hover handling

### Week 3: React Performance
- **Day 1-2**: Implement aggressive memoization
- **Day 3-4**: Reduce re-render frequency
- **Day 5**: Optimize useEffect dependencies

### Week 4: Advanced Features
- **Day 1-3**: Web Workers implementation
- **Day 4-5**: Canvas rendering (if needed)

## Success Metrics

### Target Performance Goals
- **GPU Usage**: Reduce from 100% to <30%
- **Frame Rate**: Maintain 60fps during interactions
- **Memory Usage**: <100MB for typical game states
- **Initial Load**: <2 seconds to first meaningful paint

### Monitoring Tools
1. Chrome DevTools Performance tab
2. React DevTools Profiler
3. `performance.mark()` and `performance.measure()`
4. Memory usage tracking

### Testing Strategy
```jsx
// Performance monitoring component
const PerformanceMonitor = () => {
  useEffect(() => {
    const observer = new PerformanceObserver((list) => {
      list.getEntries().forEach((entry) => {
        if (entry.entryType === 'measure') {
          console.log(`${entry.name}: ${entry.duration}ms`);
        }
      });
    });
    
    observer.observe({ entryTypes: ['measure'] });
    
    return () => observer.disconnect();
  }, []);

  return null;
};
```

## Risk Mitigation

### Rollback Plan
- Keep current implementation in separate branch
- Feature flags for new optimizations
- A/B testing with performance metrics

### Compatibility Concerns
- Test on lower-end devices
- Ensure Safari/Firefox compatibility
- Graceful degradation for older browsers

## Expected Outcomes

### Immediate (Week 1)
- 50-70% reduction in GPU usage
- Smoother interactions and scrolling
- Reduced browser heat/fan noise

### Short-term (Month 1)
- 80%+ reduction in GPU usage
- 60fps consistent performance
- Better memory efficiency

### Long-term (Month 3)
- Scalable to larger game states
- Professional-grade performance
- Foundation for future features

## Quick Implementation Guide

### Start Here (Day 1 - High Impact, Low Risk)

1. **Disable problematic animations immediately:**
```css
/* In Grid.css, comment out or remove: */
.entity-label {
  /* animation: labelPulse 3s ease-in-out infinite; */
}

.entity-label:hover {
  /* transform: scale(1.1) !important; */
  opacity: 0.8; /* Use this instead */
}
```

2. **Remove backdrop filters:**
```css
.animal-label, .zookeeper-label {
  /* backdrop-filter: blur(2px); */
  /* -webkit-backdrop-filter: blur(2px); */
  background-color: rgba(255, 255, 255, 0.95);
}
```

3. **Limit will-change usage:**
```css
.grid-container {
  /* will-change: transform; */ /* Remove this */
}
```

These three changes alone should reduce GPU usage by 40-60% immediately.

## Conclusion

This optimization plan addresses the root causes of the 100% GPU usage through systematic improvements to CSS, DOM structure, and React patterns. The phased approach ensures quick wins while building toward long-term performance excellence.

The most critical issues are the CSS animations and backdrop filters that are overwhelming the GPU compositor. By eliminating these first, you'll see immediate improvement, then can work on the more complex React optimizations. 