# Reinforcement Learning Framework Design for Zooscape Bot

## 1. State Space Representation

Based on the analysis of the game mechanics, we'll design a state representation that captures all relevant information while remaining efficient for neural network processing:

### Grid-based Representation
- **Map Layout**: Binary representation of walls (1) vs traversable spaces (0)
- **Pellet Locations**: Binary mask of pellet locations (1 = pellet present, 0 = no pellet)
- **Bot Position**: One-hot encoded position of our bot on the grid
- **Zookeeper Positions**: Binary mask of zookeeper locations
- **Other Animals**: Binary mask of other animal positions
- **Distance Features**:
  - Distance transform to nearest pellet
  - Distance transform to nearest zookeeper
  - Distance transform to cage (spawn point)

### Additional Features
- **Historical Data**:
  - Previous N positions of our bot (to detect oscillation)
  - Previous N positions of zookeeper (to detect patterns)
- **Game State Metadata**:
  - Current score
  - Number of times captured
  - Time spent on spawn
  - Distance traveled

## 2. Action Space

The action space is discrete with 4 possible actions:
- UP (1)
- DOWN (2)
- LEFT (3)
- RIGHT (4)

## 3. Reward Structure

The reward function will be designed to encourage behaviors that lead to winning the game:

### Primary Rewards
- **Pellet Collection**: +1.0 for each pellet collected
- **Capture Avoidance**: -5.0 for being captured by zookeeper
- **Survival Bonus**: +0.01 per tick survived without being captured

### Secondary Rewards (Shaping)
- **Proximity to Pellets**: +0.1 * (1/distance_to_nearest_pellet) to encourage moving toward pellets
- **Distance from Zookeeper**: +0.2 * (distance_to_zookeeper/max_distance) to encourage staying away from zookeeper
- **Exploration Bonus**: +0.05 for visiting new cells (similar to the existing visit count mechanism)
- **Oscillation Penalty**: -0.1 for revisiting the same cell within N steps
- **Movement Efficiency**: +0.02 for moving toward areas with higher pellet density

## 4. Neural Network Architecture

We'll design a neural network architecture that balances expressiveness with inference speed:

### Input Layer
- Flattened grid representation (walls, pellets, positions)
- Additional feature vector (distances, metadata)

### Hidden Layers
- 2D Convolutional layers for spatial feature extraction:
  - Conv2D(32, 3x3, activation='relu')
  - Conv2D(64, 3x3, activation='relu')
  - Flatten()
- Dense layers for decision making:
  - Dense(256, activation='relu')
  - Dense(128, activation='relu')
  - Dense(64, activation='relu')

### Output Layer
- Dense(4, activation='linear') - Q-values for each action

### Optimization for 150ms Constraint
- Use smaller network architecture with fewer parameters
- Consider quantization for faster inference
- Implement early stopping in training
- Use efficient batch processing

## 5. Training Approach

### Deep Q-Learning (DQN)
- Experience replay buffer to store (state, action, reward, next_state) tuples
- Target network for stable Q-value estimation
- ε-greedy exploration strategy with annealing

### Training Process
1. Initialize replay memory D to capacity N
2. Initialize action-value function Q with random weights
3. Initialize target action-value function Q̂ with weights θ⁻ = θ
4. For each episode:
   - Initialize state s₁
   - For each time step t:
     - With probability ε select a random action aₜ, otherwise aₜ = argmax_a Q(sₜ, a; θ)
     - Execute action aₜ, observe reward rₜ and next state sₜ₊₁
     - Store transition (sₜ, aₜ, rₜ, sₜ₊₁) in D
     - Sample random mini-batch of transitions from D
     - Perform gradient descent step on loss function
     - Every C steps, update Q̂ = Q

### Self-Play and Curriculum Learning
- Train against progressively more difficult versions of the Reference Bot
- Implement self-play to improve against different strategies
- Save checkpoints of models at different skill levels for curriculum learning

## 6. Performance Optimization

To meet the 150ms constraint:

### Model Optimization
- Quantize model weights (int8 or float16)
- Prune unnecessary connections
- Distill knowledge from larger models to smaller ones

### Inference Optimization
- Cache computed features when possible
- Implement batched prediction for efficiency
- Use TensorFlow's graph optimization and XLA compilation

### Fallback Mechanisms
- Implement a simple heuristic fallback if RL inference exceeds time budget
- Maintain a priority queue of pre-computed "safe moves" for emergency situations

## 7. Integration with Game Engine

### Interface Design
- Create a wrapper class that translates GameState to RL state representation
- Implement action selection logic with timing safeguards
- Add telemetry for monitoring decision time and model performance

### Hybrid Approach
- Combine RL policy with pathfinding for known good behaviors
- Use RL for high-level strategy and A* for tactical movement
- Implement a meta-controller to decide when to use RL vs. heuristics

## 8. Evaluation Metrics

- Win rate against Reference Bot
- Average score differential
- Capture avoidance rate
- Pellet collection efficiency
- Decision time (must be <150ms)
- Learning curve (performance over training episodes)
