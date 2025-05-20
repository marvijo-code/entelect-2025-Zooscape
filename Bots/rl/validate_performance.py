import os
import sys
import numpy as np
import tensorflow as tf
import matplotlib.pyplot as plt
import json
import time

def validate_model_performance(model_path, num_iterations=100):
    """
    Validate the performance of the optimized model against the Reference Bot
    
    Args:
        model_path: Path to the optimized model
        num_iterations: Number of iterations for benchmarking
    """
    print(f"Validating model performance: {model_path}")
    
    # Check if the model exists
    if not os.path.exists(model_path):
        print(f"Model not found: {model_path}")
        return False
    
    # Load the model
    try:
        interpreter = tf.lite.Interpreter(model_path=model_path)
        interpreter.allocate_tensors()
        
        # Get input and output details
        input_details = interpreter.get_input_details()
        output_details = interpreter.get_output_details()
        
        print("Model loaded successfully")
        print(f"Input details: {input_details}")
        print(f"Output details: {output_details}")
    except Exception as e:
        print(f"Error loading model: {e}")
        return False
    
    # Create random input data for benchmarking
    grid_shape = tuple(input_details[0]['shape'][1:])
    metadata_shape = tuple(input_details[1]['shape'][1:])
    
    print(f"Grid shape: {grid_shape}")
    print(f"Metadata shape: {metadata_shape}")
    
    # Benchmark inference time
    inference_times = []
    for i in range(num_iterations):
        # Create random input data
        grid_input = np.random.random(grid_shape).astype(np.float32)
        metadata_input = np.random.random(metadata_shape).astype(np.float32)
        
        # Set input tensors
        interpreter.set_tensor(input_details[0]['index'], np.expand_dims(grid_input, 0))
        interpreter.set_tensor(input_details[1]['index'], np.expand_dims(metadata_input, 0))
        
        # Measure inference time
        start_time = time.time()
        interpreter.invoke()
        inference_time = (time.time() - start_time) * 1000
        inference_times.append(inference_time)
        
        # Get output
        output = interpreter.get_tensor(output_details[0]['index'])
        
        # Print progress
        if (i + 1) % 10 == 0:
            print(f"Completed {i + 1}/{num_iterations} iterations")
    
    # Calculate statistics
    avg_time = sum(inference_times) / len(inference_times)
    max_time = max(inference_times)
    min_time = min(inference_times)
    p95_time = sorted(inference_times)[int(len(inference_times) * 0.95)]
    under_150ms = sum(1 for t in inference_times if t < 150) / len(inference_times) * 100
    
    # Print results
    print("\nPerformance Validation Results:")
    print(f"Average inference time: {avg_time:.2f}ms")
    print(f"Min inference time: {min_time:.2f}ms")
    print(f"Max inference time: {max_time:.2f}ms")
    print(f"95th percentile inference time: {p95_time:.2f}ms")
    print(f"Percentage under 150ms: {under_150ms:.2f}%")
    
    # Plot histogram of inference times
    plt.figure(figsize=(10, 6))
    plt.hist(inference_times, bins=20, alpha=0.7, color='blue')
    plt.axvline(x=150, color='red', linestyle='--', label='150ms Constraint')
    plt.xlabel('Inference Time (ms)')
    plt.ylabel('Frequency')
    plt.title('Inference Time Distribution')
    plt.legend()
    
    # Save plot
    os.makedirs('results', exist_ok=True)
    plot_path = 'results/inference_time_distribution.png'
    plt.savefig(plot_path)
    print(f"Inference time distribution plot saved to {plot_path}")
    
    # Save results to JSON
    results = {
        'avg_time': avg_time,
        'min_time': min_time,
        'max_time': max_time,
        'p95_time': p95_time,
        'under_150ms': under_150ms,
        'inference_times': inference_times
    }
    
    results_path = 'results/performance_validation.json'
    with open(results_path, 'w') as f:
        json.dump(results, f, indent=4)
    print(f"Performance validation results saved to {results_path}")
    
    # Check if the model meets the 150ms constraint
    if under_150ms >= 99.0:
        print("\nVALIDATION PASSED: Model meets the 150ms constraint (>99% of inferences under 150ms)")
        return True
    else:
        print("\nVALIDATION FAILED: Model does not meet the 150ms constraint (<99% of inferences under 150ms)")
        return False

def main():
    # Check if a model path was provided
    if len(sys.argv) > 1:
        model_path = sys.argv[1]
    else:
        # Find the latest optimized model in the models directory
        models_dir = 'models'
        if not os.path.exists(models_dir):
            print("Models directory not found.")
            return
            
        model_files = [f for f in os.listdir(models_dir) if f.endswith('.tflite')]
        if not model_files:
            print("No TFLite model files found.")
            return
            
        # Sort by modification time (newest first)
        model_files.sort(key=lambda x: os.path.getmtime(os.path.join(models_dir, x)), reverse=True)
        model_path = os.path.join(models_dir, model_files[0])
    
    # Validate model performance
    validate_model_performance(model_path)

if __name__ == "__main__":
    main()
