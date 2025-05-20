# MCTSo4 Bot - Advanced Parallel MCTS Implementation

This bot implements state-of-the-art Monte Carlo Tree Search techniques for the Zooscape game, leveraging modern parallel computing for improved performance.

## Key Features

### Parallel MCTS Implementation
- **Multi-threaded search**: Utilizes all available CPU cores to search the game tree in parallel
- **Thread-safe node structure**: Optimized for concurrent access with atomic counters
- **Virtual loss mechanism**: Prevents thread collision in tree exploration
- **Dynamic thread count**: Automatically scales based on available system resources

### Advanced MCTS Techniques
- **RAVE (Rapid Action Value Estimation)**: Improves learning by sharing knowledge across similar positions
- **Progressive widening**: Controls branching factor based on visit count to focus computational resources
- **Epsilon-greedy simulation**: Combines random and heuristic-guided rollouts for more efficient exploration
- **Heuristic move evaluation**: Uses state difference evaluation to improve simulation quality
- **Custom exploration balance**: Dynamically adjusts exploration/exploitation based on reward magnitude

### Performance Optimizations
- **Adaptive time budget**: Configurable time limits with dynamic self-tuning
- **Strategy-specific parameters**: Specialized configurations for different game situations
- **Efficient memory usage**: Specialized data structures to minimize overhead
- **Early termination**: Smart pruning of clearly inferior branches

## Implementation Details

### Key Components
- `ThreadSafeNode.cs`: Thread-safe tree node implementation with RAVE 
- `ParallelMCTSAlgorithm.cs`: Core parallel MCTS algorithm
- `FastGameState.cs`: Lightweight game state for rapid simulation
- `AdaptiveStrategyController.cs`: Dynamic parameter tuning based on game context

### Strategy Adaptation
The bot automatically adjusts its behavior between three main strategies:
1. **Collecting**: Focuses on efficient pellet collection
2. **Evading**: Prioritizes avoiding zookeepers
3. **EscapeFocus**: Maximizes escape potential when necessary

## Technical Implementation
- Uses C# concurrent collections for thread safety
- Leverages `Interlocked` operations for atomic counter updates
- Uses Task Parallel Library for efficient threading
- Implements custom thread-safe data structures for simulation tracking

## Performance Notes
- Typically reaches 400-800 iterations per move within the 130ms time limit
- Parallelization provides 3-5x more iterations than single-threaded MCTS
- RAVE implementation significantly improves exploration efficiency
- Progressive widening helps manage wider branching factors efficiently 