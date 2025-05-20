import numpy as np
import tensorflow as tf
import time
import os
import random
from collections import deque
import json

# Ensure TensorFlow uses memory growth to avoid consuming all GPU memory
physical_devices = tf.config.list_physical_devices("GPU")
if physical_devices:
    for device in physical_devices:
        tf.config.experimental.set_memory_growth(device, True)


class StateProcessor:
    """
    Processes GameState into a format suitable for the neural network
    """

    def __init__(self, max_grid_size=50):
        self.max_grid_size = max_grid_size
        self.visit_counts = {}  # Track cell visit counts
        self.previous_positions = deque(maxlen=5)  # Track previous positions
        self.previous_zookeeper_positions = deque(
            maxlen=5
        )  # Track previous zookeeper positions

    def reset(self):
        """Reset state tracking between episodes"""
        self.visit_counts = {}
        self.previous_positions.clear()
        self.previous_zookeeper_positions.clear()

    def process_state(self, game_state, bot_id):
        """
        Convert GameState to neural network input format

        Args:
            game_state: The game state object from the engine
            bot_id: The ID of our bot

        Returns:
            Processed state as numpy arrays
        """
        # Find our animal
        our_animal = next((a for a in game_state.Animals if a.Id == bot_id), None)
        if not our_animal:
            raise ValueError(f"Could not find animal with ID {bot_id}")

        # Get grid dimensions
        cells = game_state.Cells
        max_x = max(c.X for c in cells)
        max_y = max(c.Y for c in cells)
        grid_size = max(max_x + 1, max_y + 1)

        # Initialize grid layers
        walls = np.zeros((grid_size, grid_size), dtype=np.float32)
        pellets = np.zeros((grid_size, grid_size), dtype=np.float32)
        our_position = np.zeros((grid_size, grid_size), dtype=np.float32)
        zookeepers = np.zeros((grid_size, grid_size), dtype=np.float32)
        other_animals = np.zeros((grid_size, grid_size), dtype=np.float32)

        # Fill grid layers
        for cell in cells:
            if cell.Content == 0:  # Wall
                walls[cell.Y, cell.X] = 1
            elif cell.Content == 1:  # Pellet
                pellets[cell.Y, cell.X] = 1

        # Mark our position
        our_position[our_animal.Y, our_animal.X] = 1

        # Update visit counts and track position
        pos_key = (our_animal.X, our_animal.Y)
        self.visit_counts[pos_key] = self.visit_counts.get(pos_key, 0) + 1
        self.previous_positions.append(pos_key)

        # Mark zookeeper positions
        for zk in game_state.Zookeepers:
            zookeepers[zk.Y, zk.X] = 1
            self.previous_zookeeper_positions.append((zk.X, zk.Y))

        # Mark other animals
        for animal in game_state.Animals:
            if animal.Id != bot_id:
                other_animals[animal.Y, animal.X] = 1

        # Calculate distance features
        distance_to_pellets = self._calculate_distance_transform(~pellets.astype(bool))
        distance_to_zookeepers = self._calculate_distance_transform(
            ~zookeepers.astype(bool)
        )

        # Normalize distances
        max_distance = np.sqrt(grid_size**2 + grid_size**2)
        distance_to_pellets = distance_to_pellets / max_distance
        distance_to_zookeepers = distance_to_zookeepers / max_distance

        # Create visit count grid
        visit_grid = np.zeros((grid_size, grid_size), dtype=np.float32)
        for (x, y), count in self.visit_counts.items():
            if 0 <= y < grid_size and 0 <= x < grid_size:
                visit_grid[y, x] = min(count / 10.0, 1.0)  # Normalize visits

        # Stack all grid features
        grid_features = np.stack(
            [
                walls,
                pellets,
                our_position,
                zookeepers,
                other_animals,
                distance_to_pellets,
                distance_to_zookeepers,
                visit_grid,
            ],
            axis=-1,
        )

        # Create metadata features
        metadata = np.array(
            [
                our_animal.Score / 1000.0,  # Normalize score
                our_animal.CapturedCounter / 10.0,  # Normalize capture count
                game_state.Tick / 10000.0,  # Normalize game tick
            ],
            dtype=np.float32,
        )

        return grid_features, metadata

    def _calculate_distance_transform(self, binary_mask):
        """Simple distance transform implementation"""
        indices = np.where(~binary_mask)
        if len(indices[0]) == 0:
            return np.ones_like(binary_mask, dtype=np.float32) * np.inf

        distances = np.zeros_like(binary_mask, dtype=np.float32)
        for i in range(binary_mask.shape[0]):
            for j in range(binary_mask.shape[1]):
                if binary_mask[i, j]:
                    # Calculate Manhattan distance to nearest target
                    min_dist = float("inf")
                    for y, x in zip(indices[0], indices[1]):
                        dist = abs(i - y) + abs(j - x)
                        min_dist = min(min_dist, dist)
                    distances[i, j] = min_dist

        return distances


class DQNAgent:
    """
    Deep Q-Network agent for Zooscape
    """

    def __init__(
        self,
        state_shape,
        action_size=4,
        learning_rate=0.001,
        gamma=0.99,
        epsilon=1.0,
        epsilon_decay=0.995,
        epsilon_min=0.1,
        memory_size=10000,
        batch_size=32,
        update_target_freq=1000,
        model_dir="models",
    ):
        self.state_shape = state_shape
        self.action_size = action_size
        self.memory = deque(maxlen=memory_size)
        self.gamma = gamma  # discount factor
        self.epsilon = epsilon  # exploration rate
        self.epsilon_decay = epsilon_decay
        self.epsilon_min = epsilon_min
        self.learning_rate = learning_rate
        self.batch_size = batch_size
        self.update_target_freq = update_target_freq
        self.model_dir = model_dir
        self.train_step_counter = 0

        # Create model directory if it doesn't exist
        if not os.path.exists(model_dir):
            os.makedirs(model_dir)

        # Build models
        self.model = self._build_model()
        self.target_model = self._build_model()
        self.update_target_model()

    def _build_model(self):
        """Build the neural network model"""
        # Input layers
        grid_input = tf.keras.layers.Input(shape=self.state_shape[0])
        metadata_input = tf.keras.layers.Input(shape=self.state_shape[1])

        # Process grid features with CNN
        x = tf.keras.layers.Conv2D(32, (3, 3), activation="relu", padding="same")(
            grid_input
        )
        x = tf.keras.layers.Conv2D(64, (3, 3), activation="relu", padding="same")(x)
        x = tf.keras.layers.Flatten()(x)

        # Combine with metadata
        combined = tf.keras.layers.Concatenate()([x, metadata_input])

        # Dense layers
        x = tf.keras.layers.Dense(256, activation="relu")(combined)
        x = tf.keras.layers.Dense(128, activation="relu")(x)
        x = tf.keras.layers.Dense(64, activation="relu")(x)

        # Output layer (Q-values for each action)
        outputs = tf.keras.layers.Dense(self.action_size, activation="linear")(x)

        # Create model
        model = tf.keras.models.Model(
            inputs=[grid_input, metadata_input], outputs=outputs
        )
        model.compile(
            optimizer=tf.keras.optimizers.Adam(learning_rate=self.learning_rate),
            loss="mse",
        )

        return model

    def update_target_model(self):
        """Copy weights from model to target_model"""
        self.target_model.set_weights(self.model.get_weights())

    def remember(self, state, action, reward, next_state, done):
        """Store experience in memory"""
        self.memory.append((state, action, reward, next_state, done))

    def act(self, state, training=True):
        """Choose action based on epsilon-greedy policy"""
        if training and np.random.rand() <= self.epsilon:
            return random.randrange(self.action_size)

        start_time = time.time()
        q_values = self.model.predict(state, verbose=0)
        inference_time = time.time() - start_time

        # Log inference time for monitoring
        if inference_time > 0.1:  # Log if over 100ms
            print(f"Warning: Inference time: {inference_time*1000:.2f}ms")

        return np.argmax(q_values[0])

    def replay(self):
        """Train the model with experiences from memory"""
        if len(self.memory) < self.batch_size:
            return

        # Sample batch from memory
        minibatch = random.sample(self.memory, self.batch_size)

        # Prepare batch data
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
        metadata_next_states_reshaped = np.squeeze(
            np.array(metadata_next_states), axis=1
        )

        # Predict Q-values for current states
        q_values = self.model.predict(
            [grid_states_reshaped, metadata_states_reshaped], verbose=0
        )

        # Predict Q-values for next states using target model
        next_q_values = self.target_model.predict(
            [grid_next_states_reshaped, metadata_next_states_reshaped], verbose=0
        )

        # Update Q-values for actions taken
        for i in range(self.batch_size):
            if dones[i]:
                q_values[i][actions[i]] = rewards[i]
            else:
                q_values[i][actions[i]] = rewards[i] + self.gamma * np.max(
                    next_q_values[i]
                )

        # Train the model - use the reshaped inputs for training too
        self.model.fit(
            [grid_states_reshaped, metadata_states_reshaped],
            q_values,
            epochs=1,
            verbose=0,
            batch_size=self.batch_size,
        )

        # Update target model periodically
        self.train_step_counter += 1
        if self.train_step_counter % self.update_target_freq == 0:
            self.update_target_model()

        # Decay epsilon
        if self.epsilon > self.epsilon_min:
            self.epsilon *= self.epsilon_decay

    def load(self, name):
        """Load model weights"""
        self.model.load_weights(os.path.join(self.model_dir, name))
        self.update_target_model()

    def save(self, name):
        """Save model weights"""
        print(f"Saving model weights to {os.path.join(self.model_dir, name)}")
        self.model.save_weights(os.path.join(self.model_dir, name))


class RewardCalculator:
    """
    Calculates rewards based on game state transitions
    """

    def __init__(self):
        self.previous_score = 0
        self.previous_captured = 0

    def reset(self):
        """Reset between episodes"""
        self.previous_score = 0
        self.previous_captured = 0

    def calculate_reward(self, game_state, bot_id, state_processor):
        """
        Calculate reward based on current game state

        Args:
            game_state: Current game state
            bot_id: Our bot's ID
            state_processor: StateProcessor instance for additional context

        Returns:
            Calculated reward value
        """
        # Find our animal
        our_animal = next((a for a in game_state.Animals if a.Id == bot_id), None)
        if not our_animal:
            return -10.0  # Severe penalty if we can't find our animal

        reward = 0.0

        # Reward for score increase (collecting pellets)
        score_increase = our_animal.Score - self.previous_score
        if score_increase > 0:
            reward += 1.0 * score_increase  # +1 for each pellet

        # Penalty for being captured
        if our_animal.CapturedCounter > self.previous_captured:
            reward -= 5.0  # -5 for being captured

        # Small survival bonus
        reward += 0.01

        # Proximity to pellets reward
        cells = game_state.Cells
        pellet_cells = [c for c in cells if c.Content == 1]  # Pellet content
        if pellet_cells:
            min_distance = min(
                abs(c.X - our_animal.X) + abs(c.Y - our_animal.Y) for c in pellet_cells
            )
            reward += 0.1 * (1.0 / max(1, min_distance))

        # Distance from zookeeper reward
        for zk in game_state.Zookeepers:
            distance = abs(zk.X - our_animal.X) + abs(zk.Y - our_animal.Y)
            if distance < 3:
                reward -= 0.2 * (3 - distance)  # Penalty for being close to zookeeper
            else:
                reward += 0.05  # Small reward for being away from zookeeper

        # Exploration bonus
        pos_key = (our_animal.X, our_animal.Y)
        visit_count = state_processor.visit_counts.get(pos_key, 0)
        if visit_count <= 1:
            reward += 0.05  # Bonus for visiting new cells
        else:
            reward -= 0.02 * min(visit_count, 5)  # Penalty for revisiting cells

        # Oscillation penalty
        if len(state_processor.previous_positions) >= 4:
            last_positions = list(state_processor.previous_positions)[-4:]
            if (
                last_positions[0] == last_positions[2]
                and last_positions[1] == last_positions[3]
            ):
                reward -= 0.5  # Penalty for oscillating back and forth

        # Update previous values
        self.previous_score = our_animal.Score
        self.previous_captured = our_animal.CapturedCounter

        return reward


class RLBotService:
    """
    Main service for the RL bot that interfaces with the game engine
    """

    def __init__(self, model_path=None, training=True):
        self._bot_id = None
        self.training = training
        self.state_processor = StateProcessor()
        self.reward_calculator = RewardCalculator()

        # Initialize with dummy state shape, will be updated on first state
        self.agent = None
        self.previous_state = None
        self.previous_action = None

        self.model_path = model_path
        self.episode_rewards = []
        self.current_episode_reward = 0
        self.episode_counter = 0
        self.last_save_time = time.time()

    def SetBotId(self, bot_id):
        """Set the bot ID"""
        self._bot_id = bot_id

    def GetBotId(self):
        """Get the bot ID"""
        return self._bot_id

    def ProcessState(self, game_state):
        """
        Process game state and return bot command

        Args:
            game_state: The game state from the engine

        Returns:
            BotCommand with the selected action
        """
        start_time = time.time()

        # Initialize agent if not already done
        if self.agent is None:
            # Process state once to get dimensions
            grid_features, metadata = self.state_processor.process_state(
                game_state, self._bot_id
            )
            state_shape = [(grid_features.shape), (metadata.shape)]
            self.agent = DQNAgent(state_shape)

            # Load model if path provided
            if self.model_path and os.path.exists(self.model_path):
                self.agent.load(self.model_path)
                print(f"Loaded model from {self.model_path}")

        # Process current state
        grid_features, metadata = self.state_processor.process_state(
            game_state, self._bot_id
        )
        current_state = [np.expand_dims(grid_features, 0), np.expand_dims(metadata, 0)]

        # Calculate reward if we have a previous state
        if (
            self.previous_state is not None
            and self.previous_action is not None
            and self.training
        ):
            reward = self.reward_calculator.calculate_reward(
                game_state, self._bot_id, self.state_processor
            )

            # Check if episode ended (animal was captured)
            our_animal = next(
                (a for a in game_state.Animals if a.Id == self._bot_id), None
            )
            done = False
            if (
                our_animal
                and our_animal.CapturedCounter
                > self.reward_calculator.previous_captured
            ):
                done = True

            # Store experience in memory
            self.agent.remember(
                self.previous_state, self.previous_action, reward, current_state, done
            )

            # Update episode reward
            self.current_episode_reward += reward

            # Train the agent
            if self.training:
                self.agent.replay()

            # Reset on episode end
            if done:
                self.episode_rewards.append(self.current_episode_reward)
                self.current_episode_reward = 0
                self.episode_counter += 1
                print(
                    f"Episode {self.episode_counter} ended with reward: {self.episode_rewards[-1]:.2f}"
                )

                # Save model periodically
                current_time = time.time()
                if current_time - self.last_save_time > 300:  # Save every 5 minutes
                    self.agent.save(f"zooscape_dqn_{self.episode_counter}.h5")
                    self.last_save_time = current_time

                    # Save training stats
                    with open(
                        os.path.join(self.agent.model_dir, "training_stats.json"), "w"
                    ) as f:
                        json.dump(
                            {
                                "episode_rewards": self.episode_rewards,
                                "epsilon": self.agent.epsilon,
                            },
                            f,
                        )

        # Choose action
        action_idx = self.agent.act(current_state, training=self.training)

        # Convert to BotAction (1-based in the game)
        bot_action = action_idx + 1

        # Store state and action for next iteration
        self.previous_state = current_state
        self.previous_action = action_idx

        # Check decision time
        decision_time = (time.time() - start_time) * 1000
        if decision_time > 100:  # Log if over 100ms
            print(f"Warning: Decision time: {decision_time:.2f}ms")

        # Create and return command
        return {"Action": bot_action}
