import os
import sys
import numpy as np
import tensorflow as tf
from tensorflow.keras import layers, models, optimizers
import time

class OptimizedRLModel:
    """
    Optimized reinforcement learning model for Zooscape with TensorFlow Lite conversion
    to ensure decisions within the 150ms constraint
    """
    def __init__(self, grid_shape=(30, 30, 8), metadata_shape=(3,), action_size=4):
        self.grid_shape = grid_shape
        self.metadata_shape = metadata_shape
        self.action_size = action_size
        self.model = self._build_model()
        self.tflite_model = None
        self.interpreter = None
        
    def _build_model(self):
        """Build a neural network model optimized for inference speed"""
        # Input layers
        grid_input = layers.Input(shape=self.grid_shape)
        metadata_input = layers.Input(shape=self.metadata_shape)
        
        # Process grid features with lightweight CNN
        # Use separable convolutions for efficiency
        x = layers.SeparableConv2D(8, (3, 3), strides=(2, 2), activation='relu', padding='same')(grid_input)
        x = layers.SeparableConv2D(16, (3, 3), strides=(2, 2), activation='relu', padding='same')(x)
        x = layers.GlobalAveragePooling2D()(x)  # More efficient than Flatten
        
        # Combine with metadata using minimal dense layers
        combined = layers.Concatenate()([x, metadata_input])
        
        # Smaller dense layers
        x = layers.Dense(64, activation='relu')(combined)
        
        # Output layer (Q-values for each action)
        outputs = layers.Dense(self.action_size, activation='linear')(x)
        
        # Create model
        model = models.Model(inputs=[grid_input, metadata_input], outputs=outputs)
        model.compile(optimizer=optimizers.Adam(learning_rate=0.001), loss='mse')
        
        return model
    
    def convert_to_tflite(self):
        """Convert the model to TensorFlow Lite for faster inference"""
        # Create a converter
        converter = tf.lite.TFLiteConverter.from_keras_model(self.model)
        
        # Enable optimizations
        converter.optimizations = [tf.lite.Optimize.DEFAULT]
        
        # Convert the model
        self.tflite_model = converter.convert()
        
        # Save the model to a file
        with open('models/optimized_model.tflite', 'wb') as f:
            f.write(self.tflite_model)
            
        # Initialize the interpreter
        self.interpreter = tf.lite.Interpreter(model_content=self.tflite_model)
        self.interpreter.allocate_tensors()
        
        # Get input and output details
        self.input_details = self.interpreter.get_input_details()
        self.output_details = self.interpreter.get_output_details()
        
        print("Model converted to TensorFlow Lite and saved to models/optimized_model.tflite")
        
    def predict(self, grid_state, metadata_state):
        """Make a prediction using the optimized TFLite model"""
        if self.interpreter is None:
            # Fall back to regular model if TFLite model is not available
            return self.model.predict([np.expand_dims(grid_state, 0), 
                                      np.expand_dims(metadata_state, 0)], verbose=0)[0]
        
        # Set input tensors
        self.interpreter.set_tensor(self.input_details[0]['index'], 
                                   np.expand_dims(grid_state, 0).astype(np.float32))
        self.interpreter.set_tensor(self.input_details[1]['index'], 
                                   np.expand_dims(metadata_state, 0).astype(np.float32))
        
        # Run inference
        self.interpreter.invoke()
        
        # Get output tensor
        output = self.interpreter.get_tensor(self.output_details[0]['index'])
        
        return output[0]
    
    def load_weights(self, filepath):
        """Load weights from a file"""
        self.model.load_weights(filepath)
        # Reconvert to TFLite after loading weights
        self.convert_to_tflite()
        
    def save_weights(self, filepath):
        """Save weights to a file"""
        self.model.save_weights(filepath)

def benchmark_model(model_path, num_iterations=100):
    """Benchmark the model's inference time"""
    # Create a random state for testing
    grid_state = np.random.random((30, 30, 8)).astype(np.float32)
    metadata_state = np.random.random(3).astype(np.float32)
    
    # Create and load the optimized model
    model = OptimizedRLModel()
    model.load_weights(model_path)
    
    # Warm up
    for _ in range(10):
        _ = model.predict(grid_state, metadata_state)
    
    # Benchmark regular model
    regular_times = []
    for _ in range(num_iterations):
        start_time = time.time()
        _ = model.model.predict([np.expand_dims(grid_state, 0), 
                               np.expand_dims(metadata_state, 0)], verbose=0)
        elapsed_time = (time.time() - start_time) * 1000
        regular_times.append(elapsed_time)
    
    # Benchmark TFLite model
    tflite_times = []
    for _ in range(num_iterations):
        start_time = time.time()
        _ = model.predict(grid_state, metadata_state)
        elapsed_time = (time.time() - start_time) * 1000
        tflite_times.append(elapsed_time)
    
    # Calculate statistics
    avg_regular = sum(regular_times) / len(regular_times)
    max_regular = max(regular_times)
    min_regular = min(regular_times)
    
    avg_tflite = sum(tflite_times) / len(tflite_times)
    max_tflite = max(tflite_times)
    min_tflite = min(tflite_times)
    
    under_150ms_regular = sum(1 for t in regular_times if t < 150) / len(regular_times) * 100
    under_150ms_tflite = sum(1 for t in tflite_times if t < 150) / len(tflite_times) * 100
    
    # Print results
    print("\nBenchmark Results:")
    print(f"Regular Model - Avg: {avg_regular:.2f}ms, Min: {min_regular:.2f}ms, Max: {max_regular:.2f}ms")
    print(f"TFLite Model - Avg: {avg_tflite:.2f}ms, Min: {min_tflite:.2f}ms, Max: {max_tflite:.2f}ms")
    print(f"Regular Model - Under 150ms: {under_150ms_regular:.2f}%")
    print(f"TFLite Model - Under 150ms: {under_150ms_tflite:.2f}%")
    print(f"Speedup: {avg_regular / avg_tflite:.2f}x")
    
    return {
        'regular': {
            'avg': avg_regular,
            'min': min_regular,
            'max': max_regular,
            'under_150ms': under_150ms_regular
        },
        'tflite': {
            'avg': avg_tflite,
            'min': min_tflite,
            'max': max_tflite,
            'under_150ms': under_150ms_tflite
        },
        'speedup': avg_regular / avg_tflite
    }

def optimize_existing_model(model_path, output_path=None):
    """Optimize an existing model for faster inference"""
    if output_path is None:
        output_path = model_path.replace('.weights.h5', '_optimized.weights.h5')
    
    # Create and load the optimized model
    model = OptimizedRLModel()
    
    # Try to load the existing model weights
    try:
        model.load_weights(model_path)
        print(f"Loaded weights from {model_path}")
    except Exception as e:
        print(f"Error loading weights: {e}")
        return None
    
    # Convert to TFLite
    model.convert_to_tflite()
    
    # Save the optimized model
    model.save_weights(output_path)
    print(f"Optimized model saved to {output_path}")
    
    # Benchmark the model
    results = benchmark_model(output_path)
    
    return results

def main():
    # Check if a model path was provided
    if len(sys.argv) > 1:
        model_path = sys.argv[1]
    else:
        # Find the latest model in the models directory
        models_dir = 'models'
        if not os.path.exists(models_dir):
            print("Models directory not found.")
            return
            
        model_files = [f for f in os.listdir(models_dir) if f.endswith('.weights.h5')]
        if not model_files:
            print("No model files found.")
            return
            
        # Sort by modification time (newest first)
        model_files.sort(key=lambda x: os.path.getmtime(os.path.join(models_dir, x)), reverse=True)
        model_path = os.path.join(models_dir, model_files[0])
    
    print(f"Optimizing model: {model_path}")
    
    # Optimize the model
    results = optimize_existing_model(model_path)
    
    if results:
        print("\nOptimization complete!")
        print(f"The optimized model is {results['speedup']:.2f}x faster than the original.")
        print(f"Average inference time: {results['tflite']['avg']:.2f}ms")
        print(f"Percentage under 150ms: {results['tflite']['under_150ms']:.2f}%")

if __name__ == "__main__":
    main()
