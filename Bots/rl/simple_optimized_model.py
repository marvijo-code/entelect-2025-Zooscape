import numpy as np
import tensorflow as tf
import time
import os
import random
from collections import deque

class SimpleOptimizedModel:
    """
    A simplified model for Zooscape that meets the 150ms constraint
    without requiring additional optimization libraries
    """
    def __init__(self, state_shape, action_size=4, model_path=None):
        self.state_shape = state_shape
        self.action_size = action_size
        self.model = self._build_model()
        
        # Load model if path provided
        if model_path and os.path.exists(model_path):
            try:
                self.model.load_weights(model_path)
                print(f"Loaded model from {model_path}")
            except:
                print(f"Could not load weights from {model_path} due to architecture differences")
            
    def _build_model(self):
        """Build a smaller, more efficient neural network model"""
        # Input layers
        grid_input = tf.keras.layers.Input(shape=self.state_shape[0])
        metadata_input = tf.keras.layers.Input(shape=self.state_shape[1])
        
        # Process grid features with smaller CNN
        # Use smaller filters and strides to reduce computation
        x = tf.keras.layers.Conv2D(8, (3, 3), strides=(2, 2), activation='relu', padding='same')(grid_input)
        x = tf.keras.layers.Conv2D(16, (3, 3), strides=(2, 2), activation='relu', padding='same')(x)
        x = tf.keras.layers.Flatten()(x)
        
        # Combine with metadata
        combined = tf.keras.layers.Concatenate()([x, metadata_input])
        
        # Smaller dense layers
        x = tf.keras.layers.Dense(64, activation='relu')(combined)
        
        # Output layer (Q-values for each action)
        outputs = tf.keras.layers.Dense(self.action_size, activation='linear')(x)
        
        # Create model
        model = tf.keras.models.Model(inputs=[grid_input, metadata_input], outputs=outputs)
        model.compile(optimizer=tf.keras.optimizers.Adam(learning_rate=0.001), loss='mse')
        
        return model
    
    def benchmark_inference(self, num_tests=100):
        """Benchmark inference time"""
        # Generate random test data
        grid_features = np.random.rand(1, 30, 30, 8).astype(np.float32)
        metadata = np.random.rand(1, 3).astype(np.float32)
        
        # Warm-up
        for _ in range(10):
            _ = self.model.predict([grid_features, metadata], verbose=0)
            
        # Benchmark
        inference_times = []
        for _ in range(num_tests):
            start_time = time.time()
            _ = self.model.predict([grid_features, metadata], verbose=0)
            inference_time = (time.time() - start_time) * 1000  # Convert to ms
            inference_times.append(inference_time)
            
        avg_time = np.mean(inference_times)
        max_time = np.max(inference_times)
        min_time = np.min(inference_times)
        
        print(f"Inference time (ms): Avg={avg_time:.2f}, Min={min_time:.2f}, Max={max_time:.2f}")
        print(f"Percentage of inferences under 150ms: {(np.array(inference_times) < 150).mean() * 100:.2f}%")
        
        return avg_time, max_time, min_time
    
    def save(self, filepath):
        """Save the model"""
        # Ensure filepath ends with .weights.h5 as required by Keras
        if not filepath.endswith('.weights.h5'):
            filepath = filepath.replace('.h5', '.weights.h5')
        self.model.save_weights(filepath)
        print(f"Model saved to {filepath}")
    
    def predict(self, state):
        """Make a prediction with timing"""
        start_time = time.time()
        grid_features, metadata = state
        
        # Ensure correct shape
        if len(grid_features.shape) == 4 and grid_features.shape[0] == 1:
            # Already batched correctly
            pass
        elif len(grid_features.shape) == 3:
            # Add batch dimension
            grid_features = np.expand_dims(grid_features, 0)
        
        if len(metadata.shape) == 2 and metadata.shape[0] == 1:
            # Already batched correctly
            pass
        elif len(metadata.shape) == 1:
            # Add batch dimension
            metadata = np.expand_dims(metadata, 0)
            
        # Make prediction
        q_values = self.model.predict([grid_features, metadata], verbose=0)
        
        # Log inference time if it's close to the limit
        inference_time = (time.time() - start_time) * 1000
        if inference_time > 100:
            print(f"Warning: Inference time: {inference_time:.2f}ms")
            
        return q_values[0]  # Return unbatched result

# Fallback heuristic for emergency situations
class HeuristicFallback:
    """Simple heuristic fallback for when inference time might exceed 150ms"""
    
    def __init__(self):
        self.previous_action = None
        self.action_count = 0
        
    def predict(self, state):
        """
        Make a quick heuristic decision based on simple rules
        
        Args:
            state: [grid_features, metadata] - Same format as RL model input
            
        Returns:
            Q-values for actions (higher is better)
        """
        grid_features, _ = state
        
        # Extract relevant information from grid
        if len(grid_features.shape) == 4:
            grid = grid_features[0]  # Remove batch dimension
        else:
            grid = grid_features
            
        # Extract layers
        walls = grid[:, :, 0]
        pellets = grid[:, :, 1]
        our_position = grid[:, :, 2]
        zookeepers = grid[:, :, 3]
        
        # Find our position
        pos_y, pos_x = np.where(our_position > 0.5)
        if len(pos_y) == 0 or len(pos_x) == 0:
            # Can't find position, return random action
            return np.random.rand(4)
            
        pos_y, pos_x = pos_y[0], pos_x[0]
        
        # Initialize Q-values
        q_values = np.zeros(4)
        
        # Check each direction (UP, DOWN, LEFT, RIGHT)
        directions = [(-1, 0), (1, 0), (0, -1), (0, 1)]
        
        for i, (dy, dx) in enumerate(directions):
            ny, nx = pos_y + dy, pos_x + dx
            
            # Check if out of bounds
            if ny < 0 or ny >= grid.shape[0] or nx < 0 or nx >= grid.shape[1]:
                q_values[i] = -10  # Heavily penalize out of bounds
                continue
                
            # Check if wall
            if walls[ny, nx] > 0.5:
                q_values[i] = -10  # Heavily penalize walls
                continue
                
            # Reward for pellets
            if pellets[ny, nx] > 0.5:
                q_values[i] += 5
                
            # Penalty for zookeepers
            if zookeepers[ny, nx] > 0.5:
                q_values[i] -= 10
                
            # Check for nearby zookeepers (Manhattan distance <= 2)
            for zy in range(max(0, ny-2), min(grid.shape[0], ny+3)):
                for zx in range(max(0, nx-2), min(grid.shape[1], nx+3)):
                    if zookeepers[zy, zx] > 0.5:
                        dist = abs(zy - ny) + abs(zx - nx)
                        q_values[i] -= (3 - dist) * 2  # Closer zookeepers are worse
        
        # Avoid repeating the same action too many times
        if self.previous_action is not None:
            if i == self.previous_action:
                self.action_count += 1
                if self.action_count > 3:
                    q_values[i] -= 2  # Penalize repeating the same action
            else:
                self.action_count = 0
                
        # Avoid reversing direction
        if self.previous_action is not None:
            opposite = {0: 1, 1: 0, 2: 3, 3: 2}
            q_values[opposite[self.previous_action]] -= 1
            
        # Update previous action
        self.previous_action = np.argmax(q_values)
        
        return q_values
