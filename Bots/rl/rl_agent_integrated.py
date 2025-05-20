import numpy as np
import tensorflow as tf
import time
import os
import random
from collections import deque

class StateProcessor:
    """
    Processes the game state into a format suitable for the RL model
    """
    def __init__(self, grid_size=30):
        self.grid_size = grid_size
        
    def process_state(self, game_state):
        """
        Convert game state to neural network input format
        
        Args:
            game_state: GameState object from the engine
            
        Returns:
            Tuple of (grid_features, metadata)
        """
        # Initialize grid features with multiple channels
        # Channel 0: Walls
        # Channel 1: Pellets
        # Channel 2: Our position
        # Channel 3: Zookeepers
        # Channel 4: Distance to nearest pellet
        # Channel 5: Distance to nearest zookeeper
        # Channel 6: Visited cells (exploration map)
        # Channel 7: Reference bot position
        grid_features = np.zeros((self.grid_size, self.grid_size, 8), dtype=np.float32)
        
        # Process map cells
        for y in range(game_state.Map.Height):
            for x in range(game_state.Map.Width):
                cell = game_state.Map.Cells[y, x]
                
                # Walls
                if cell.Content == CellContent.Wall:
                    grid_features[y, x, 0] = 1.0
                
                # Pellets
                if cell.Content == CellContent.Pellet:
                    grid_features[y, x, 1] = 1.0
        
        # Find our animal
        our_animal = None
        ref_bot_pos = None
        for animal in game_state.Animals:
            if animal.BotId == "RLBot":
                our_animal = animal
                grid_features[animal.Position.Y, animal.Position.X, 2] = 1.0
            elif animal.BotId == "RefBot":
                ref_bot_pos = (animal.Position.X, animal.Position.Y)
                grid_features[animal.Position.Y, animal.Position.X, 7] = 1.0
        
        # Process zookeepers
        for zookeeper in game_state.Zookeepers:
            grid_features[zookeeper.Position.Y, zookeeper.Position.X, 3] = 1.0
        
        # Calculate distance transforms
        grid_features[:, :, 4] = self._distance_transform(grid_features[:, :, 1])  # Distance to pellets
        grid_features[:, :, 5] = self._distance_transform(grid_features[:, :, 3])  # Distance to zookeepers
        
        # Update visited cells map (for exploration)
        if our_animal is not None:
            self._update_visited_map(grid_features[:, :, 6], our_animal.Position)
        
        # Extract metadata features
        metadata = np.zeros(3, dtype=np.float32)
        metadata[0] = game_state.Tick / 1000.0  # Normalized tick count
        metadata[1] = len(game_state.Animals)   # Number of animals
        metadata[2] = len(game_state.Zookeepers) # Number of zookeepers
        
        # Reshape for model input
        grid_features_batch = np.expand_dims(grid_features, 0)
        metadata_batch = np.expand_dims(metadata, 0)
        
        return grid_features_batch, metadata_batch
    
    def _distance_transform(self, binary_grid):
        """
        Calculate distance transform from binary grid
        (distance to nearest 1 value)
        """
        # Find positions of 1s
        y_indices, x_indices = np.where(binary_grid > 0.5)
        
        if len(y_indices) == 0:
            # If no targets, return max distance
            return np.ones((self.grid_size, self.grid_size)) * self.grid_size
        
        # Initialize distance grid with max values
        distance_grid = np.ones((self.grid_size, self.grid_size)) * self.grid_size
        
        # Calculate Manhattan distance to each target and take minimum
        for y in range(self.grid_size):
            for x in range(self.grid_size):
                for target_y, target_x in zip(y_indices, x_indices):
                    dist = abs(y - target_y) + abs(x - target_x)
                    distance_grid[y, x] = min(distance_grid[y, x], dist)
        
        # Normalize distances
        if np.max(distance_grid) > 0:
            distance_grid = distance_grid / self.grid_size
            
        return distance_grid
    
    def _update_visited_map(self, visited_map, position):
        """Update the visited cells map"""
        # Decay existing values
        visited_map *= 0.99
        
        # Mark current position as visited
        visited_map[position.Y, position.X] = 1.0

class RewardCalculator:
    """
    Calculates rewards for reinforcement learning
    """
    def __init__(self):
        self.previous_state = None
        self.previous_pellet_count = 0
        self.previous_position = None
        
    def calculate_reward(self, game_state):
        """
        Calculate reward based on game state
        
        Args:
            game_state: GameState object from the engine
            
        Returns:
            float: Reward value
        """
        reward = 0.0
        
        # Find our animal
        our_animal = None
        for animal in game_state.Animals:
            if animal.BotId == "RLBot":
                our_animal = animal
                break
        
        if our_animal is None:
            return -5.0  # Heavy penalty if we're not in the game
        
        # Count pellets
        current_pellet_count = 0
        for y in range(game_state.Map.Height):
            for x in range(game_state.Map.Width):
                if game_state.Map.Cells[y, x].Content == CellContent.Pellet:
                    current_pellet_count += 1
        
        # Reward for collecting pellets
        if self.previous_pellet_count > 0:
            pellets_collected = self.previous_pellet_count - current_pellet_count
            if pellets_collected > 0:
                reward += pellets_collected * 1.0
        
        # Penalty for being near zookeepers
        for zookeeper in game_state.Zookeepers:
            distance = abs(our_animal.Position.X - zookeeper.Position.X) + \
                      abs(our_animal.Position.Y - zookeeper.Position.Y)
            if distance <= 2:
                reward -= (3 - distance) * 0.5  # Closer zookeepers are worse
        
        # Small reward for exploration (moving to new cells)
        if self.previous_position is not None:
            if (our_animal.Position.X != self.previous_position.X or 
                our_animal.Position.Y != self.previous_position.Y):
                reward += 0.05
        
        # Update previous state
        self.previous_pellet_count = current_pellet_count
        self.previous_position = our_animal.Position
        
        return reward
    
    def reset(self):
        """Reset the reward calculator for a new episode"""
        self.previous_state = None
        self.previous_pellet_count = 0
        self.previous_position = None

class DQNAgent:
    """
    Deep Q-Network agent for reinforcement learning
    """
    def __init__(self, state_shape, action_size=4, memory_size=10000, 
                 gamma=0.95, epsilon=1.0, epsilon_min=0.1, epsilon_decay=0.995,
                 learning_rate=0.001, batch_size=32, update_target_freq=100):
        self.state_shape = state_shape
        self.action_size = action_size
        self.memory = deque(maxlen=memory_size)
        self.gamma = gamma  # Discount factor
        self.epsilon = epsilon  # Exploration rate
        self.epsilon_min = epsilon_min
        self.epsilon_decay = epsilon_decay
        self.learning_rate = learning_rate
        self.batch_size = batch_size
        self.update_target_freq = update_target_freq
        self.train_step_counter = 0
        
        # Build model
        self.model = self._build_model()
        self.target_model = self._build_model()
        self.update_target_model()
        
    def _build_model(self):
        """Build a neural network model for DQN"""
        # Input layers
        grid_input = tf.keras.layers.Input(shape=self.state_shape[0][1:])
        metadata_input = tf.keras.layers.Input(shape=self.state_shape[1][1:])
        
        # Process grid features with CNN
        x = tf.keras.layers.Conv2D(16, (3, 3), strides=(2, 2), activation='relu', padding='same')(grid_input)
        x = tf.keras.layers.Conv2D(32, (3, 3), strides=(2, 2), activation='relu', padding='same')(x)
        x = tf.keras.layers.Flatten()(x)
        
        # Combine with metadata
        combined = tf.keras.layers.Concatenate()([x, metadata_input])
        
        # Dense layers
        x = tf.keras.layers.Dense(128, activation='relu')(combined)
        x = tf.keras.layers.Dense(64, activation='relu')(x)
        
        # Output layer (Q-values for each action)
        outputs = tf.keras.layers.Dense(self.action_size, activation='linear')(x)
        
        # Create model
        model = tf.keras.models.Model(inputs=[grid_input, metadata_input], outputs=outputs)
        model.compile(optimizer=tf.keras.optimizers.Adam(learning_rate=self.learning_rate), loss='mse')
        
        return model
    
    def update_target_model(self):
        """Update target model with weights from main model"""
        self.target_model.set_weights(self.model.get_weights())
    
    def remember(self, state, action, reward, next_state, done):
        """Store experience in memory"""
        self.memory.append((state, action, reward, next_state, done))
    
    def act(self, state, training=True):
        """Choose action using epsilon-greedy policy"""
        # Measure inference time
        start_time = time.time()
        
        if training and np.random.rand() <= self.epsilon:
            # Random action for exploration
            action = random.randrange(self.action_size)
        else:
            # Predict Q-values and choose best action
            grid_features, metadata = state
            q_values = self.model.predict([grid_features, metadata], verbose=0)
            action = np.argmax(q_values[0])
        
        # Log inference time if it's close to the limit
        inference_time = (time.time() - start_time) * 1000
        if inference_time > 100:
            print(f"Warning: Inference time: {inference_time:.2f}ms")
            
        return action
    
    def replay(self):
        """Train the model with experiences from memory"""
        if len(self.memory) < self.batch_size:
            return
        
        # Sample batch from memory
        minibatch = random.sample(self.memory, self.batch_size)
        
        # Extract batch components
        grid_states = []
        metadata_states = []
        grid_next_states = []
        metadata_next_states = []
        actions = []
        rewards = []
        dones = []
        
        for state, action, reward, next_state, done in minibatch:
            grid_states.append(state[0])
            metadata_states.append(state[1])
            grid_next_states.append(next_state[0])
            metadata_next_states.append(next_state[1])
            actions.append(action)
            rewards.append(reward)
            dones.append(done)
        
        # Convert to numpy arrays
        grid_states = np.array(grid_states)
        metadata_states = np.array(metadata_states)
        grid_next_states = np.array(grid_next_states)
        metadata_next_states = np.array(metadata_next_states)
        
        # Reshape inputs to remove extra dimension
        grid_states_reshaped = np.squeeze(np.array(grid_states), axis=1)
        metadata_states_reshaped = np.squeeze(np.array(metadata_states), axis=1)
        grid_next_states_reshaped = np.squeeze(np.array(grid_next_states), axis=1)
        metadata_next_states_reshaped = np.squeeze(np.array(metadata_next_states), axis=1)
        
        # Predict Q-values for current states
        q_values = self.model.predict([grid_states_reshaped, metadata_states_reshaped], verbose=0)
        
        # Predict Q-values for next states using target model
        next_q_values = self.target_model.predict(
            [grid_next_states_reshaped, metadata_next_states_reshaped], verbose=0)
        
        # Update Q-values for actions taken
        for i in range(self.batch_size):
            if dones[i]:
                q_values[i][actions[i]] = rewards[i]
            else:
                q_values[i][actions[i]] = rewards[i] + self.gamma * np.max(next_q_values[i])
                
        # Train the model - use the reshaped inputs for training too
        self.model.fit([grid_states_reshaped, metadata_states_reshaped], q_values, 
                       epochs=1, verbose=0, batch_size=self.batch_size)
        
        # Update target model periodically
        self.train_step_counter += 1
        if self.train_step_counter % self.update_target_freq == 0:
            self.update_target_model()
        
        # Decay epsilon
        if self.epsilon > self.epsilon_min:
            self.epsilon *= self.epsilon_decay
    
    def load(self, filepath):
        """Load model weights"""
        self.model.load_weights(filepath)
        self.update_target_model()
    
    def save(self, filepath):
        """Save model weights"""
        self.model.save_weights(filepath)

class RLBotService:
    """
    Service that integrates the RL agent with the game engine
    """
    def __init__(self, model_path=None):
        # Initialize components
        self.state_processor = StateProcessor()
        self.reward_calculator = RewardCalculator()
        
        # Define state shape
        grid_shape = (1, 30, 30, 8)
        metadata_shape = (1, 3)
        
        # Initialize agent
        self.agent = DQNAgent([(grid_shape), (metadata_shape)])
        
        # Load model if provided
        if model_path and os.path.exists(model_path):
            self.agent.load(model_path)
            print(f"Loaded model from {model_path}")
        
        # Game state tracking
        self.previous_state = None
        self.previous_action = None
        self.episode_reward = 0
        self.tick_count = 0
        
        # Fallback mechanism for emergency situations
        self.fallback = SimpleHeuristicFallback()
        
    def get_next_action(self, game_state):
        """
        Process game state and return next action
        
        Args:
            game_state: GameState object from the engine
            
        Returns:
            BotAction: Action to take
        """
        start_time = time.time()
        
        # Process state
        current_state = self.state_processor.process_state(game_state)
        
        # Calculate reward if we have a previous state
        reward = 0
        if self.previous_state is not None:
            reward = self.reward_calculator.calculate_reward(game_state)
            self.episode_reward += reward
        
        # Choose action
        try:
            # Try using the RL model with time monitoring
            action = self.agent.act(current_state)
            
            # Check if we're close to the time limit
            elapsed_time = (time.time() - start_time) * 1000
            if elapsed_time > 120:  # If we've used 80% of our budget, use fallback
                print(f"Warning: Switching to fallback. RL inference took {elapsed_time:.2f}ms")
                action = self.fallback.get_action(game_state)
        except Exception as e:
            # If any error occurs, use fallback
            print(f"Error in RL model: {e}. Using fallback.")
            action = self.fallback.get_action(game_state)
        
        # Store experience if we have a previous state
        if self.previous_state is not None and self.previous_action is not None:
            done = False  # In a real game, we'd set this based on game end
            self.agent.remember(self.previous_state, self.previous_action, 
                               reward, current_state, done)
            
            # Train the model periodically
            if self.tick_count % 10 == 0:
                self.agent.replay()
        
        # Update previous state and action
        self.previous_state = current_state
        self.previous_action = action
        self.tick_count += 1
        
        # Convert action index to BotAction
        # 0: UP, 1: DOWN, 2: LEFT, 3: RIGHT
        direction = BotAction.Up
        if action == 1:
            direction = BotAction.Down
        elif action == 2:
            direction = BotAction.Left
        elif action == 3:
            direction = BotAction.Right
        
        # Log total processing time
        total_time = (time.time() - start_time) * 1000
        if total_time > 130:
            print(f"Warning: Total processing time: {total_time:.2f}ms")
        
        return direction
    
    def save_model(self, filepath):
        """Save the current model"""
        self.agent.save(filepath)
        print(f"Model saved to {filepath}")
    
    def reset(self):
        """Reset for a new episode"""
        self.previous_state = None
        self.previous_action = None
        self.episode_reward = 0
        self.reward_calculator.reset()

class SimpleHeuristicFallback:
    """
    Simple heuristic-based fallback for emergency situations
    when the RL model might exceed the time constraint
    """
    def __init__(self):
        self.previous_action = None
        self.action_count = 0
    
    def get_action(self, game_state):
        """
        Get action based on simple heuristics
        
        Args:
            game_state: GameState object from the engine
            
        Returns:
            int: Action index (0: UP, 1: DOWN, 2: LEFT, 3: RIGHT)
        """
        # Find our animal
        our_animal = None
        for animal in game_state.Animals:
            if animal.BotId == "RLBot":
                our_animal = animal
                break
        
        if our_animal is None:
            return random.randint(0, 3)
        
        # Initialize scores for each direction
        scores = [0, 0, 0, 0]  # UP, DOWN, LEFT, RIGHT
        
        # Directions to check
        directions = [
            (-1, 0),  # UP
            (1, 0),   # DOWN
            (0, -1),  # LEFT
            (0, 1)    # RIGHT
        ]
        
        # Check each direction
        for i, (dy, dx) in enumerate(directions):
            ny, nx = our_animal.Position.Y + dy, our_animal.Position.X + dx
            
            # Check if out of bounds
            if ny < 0 or ny >= game_state.Map.Height or nx < 0 or nx >= game_state.Map.Width:
                scores[i] = -100  # Heavily penalize out of bounds
                continue
            
            # Check if wall
            if game_state.Map.Cells[ny, nx].Content == CellContent.Wall:
                scores[i] = -100  # Heavily penalize walls
                continue
            
            # Reward for pellets
            if game_state.Map.Cells[ny, nx].Content == CellContent.Pellet:
                scores[i] += 10
            
            # Penalty for zookeepers
            for zookeeper in game_state.Zookeepers:
                if zookeeper.Position.X == nx and zookeeper.Position.Y == ny:
                    scores[i] -= 100  # Heavily penalize moving into zookeepers
                
                # Penalty for being near zookeepers
                distance = abs(nx - zookeeper.Position.X) + abs(ny - zookeeper.Position.Y)
                if distance <= 3:
                    scores[i] -= (4 - distance) * 5  # Closer zookeepers are worse
            
            # Look for pellets in this direction (up to 5 steps away)
            for dist in range(1, 6):
                check_y, check_x = ny + dy * dist, nx + dx * dist
                
                # Check bounds
                if (check_y < 0 or check_y >= game_state.Map.Height or 
                    check_x < 0 or check_x >= game_state.Map.Width):
                    break
                
                # Check for walls
                if game_state.Map.Cells[check_y, check_x].Content == CellContent.Wall:
                    break
                
                # Reward for pellets (closer ones are better)
                if game_state.Map.Cells[check_y, check_x].Content == CellContent.Pellet:
                    scores[i] += 5 / dist
        
        # Avoid repeating the same action too many times
        if self.previous_action is not None:
            if self.previous_action == np.argmax(scores):
                self.action_count += 1
                if self.action_count > 5:
                    scores[self.previous_action] -= 5  # Penalize repeating the same action
            else:
                self.action_count = 0
        
        # Choose best action
        best_action = np.argmax(scores)
        self.previous_action = best_action
        
        return best_action
