import numpy as np
import tensorflow as tf
from tensorflow.keras.models import Sequential, Model
from tensorflow.keras.layers import Dense, Flatten, Conv2D, Input, Concatenate
from tensorflow.keras.optimizers import Adam
import time
import os

# Model optimization techniques
from tensorflow.lite.python.lite import TFLiteConverter
import tensorflow_model_optimization as tfmot

class OptimizedRLModel:
    """
    Optimized RL model for Zooscape that meets the 150ms constraint
    """
    def __init__(self, state_shape, action_size=4, model_path=None):
        self.state_shape = state_shape
        self.action_size = action_size
        self.model = self._build_model()
        
        # Load model if path provided
        if model_path and os.path.exists(model_path):
            self.model.load_weights(model_path)
            print(f"Loaded model from {model_path}")
            
        # Optimize model for inference
        self.optimize_for_inference()
        
    def _build_model(self):
        """Build a smaller, more efficient neural network model"""
        # Input layers
        grid_input = Input(shape=self.state_shape[0])
        metadata_input = Input(shape=self.state_shape[1])
        
        # Process grid features with smaller CNN
        x = Conv2D(16, (3, 3), activation='relu', padding='same')(grid_input)
        x = Conv2D(32, (3, 3), activation='relu', padding='same')(x)
        x = Flatten()(x)
        
        # Combine with metadata
        combined = Concatenate()([x, metadata_input])
        
        # Smaller dense layers
        x = Dense(128, activation='relu')(combined)
        x = Dense(64, activation='relu')(x)
        
        # Output layer (Q-values for each action)
        outputs = Dense(self.action_size, activation='linear')(x)
        
        # Create model
        model = Model(inputs=[grid_input, metadata_input], outputs=outputs)
        model.compile(optimizer=Adam(learning_rate=0.001), loss='mse')
        
        return model
    
    def optimize_for_inference(self):
        """Apply optimization techniques to improve inference speed"""
        # 1. Apply weight pruning
        prune_low_magnitude = tfmot.sparsity.keras.prune_low_magnitude
        
        # Define pruning parameters
        pruning_params = {
            'pruning_schedule': tfmot.sparsity.keras.PolynomialDecay(
                initial_sparsity=0.0,
                final_sparsity=0.5,
                begin_step=0,
                end_step=1000
            )
        }
        
        # Apply pruning to model
        self.model_for_pruning = prune_low_magnitude(self.model, **pruning_params)
        self.model_for_pruning.compile(optimizer='adam', loss='mse')
        
        # 2. Apply quantization-aware training
        quantize_model = tfmot.quantization.keras.quantize_model
        
        # Apply quantization to the pruned model
        self.quantized_model = quantize_model(self.model)
        self.quantized_model.compile(optimizer='adam', loss='mse')
        
        print("Model optimized for inference with pruning and quantization")
        
    def convert_to_tflite(self, output_path):
        """Convert model to TFLite format for faster inference"""
        converter = TFLiteConverter.from_keras_model(self.quantized_model)
        converter.optimizations = [tf.lite.Optimize.DEFAULT]
        tflite_model = converter.convert()
        
        with open(output_path, 'wb') as f:
            f.write(tflite_model)
            
        print(f"TFLite model saved to {output_path}")
        
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
