import numpy as np
import tensorflow as tf
import time
import os
import matplotlib.pyplot as plt
from simple_optimized_model import SimpleOptimizedModel, HeuristicFallback

def benchmark_and_optimize():
    """
    Benchmark and optimize the RL model to meet the 150ms constraint
    """
    print("Starting model benchmarking and optimization...")
    
    # Create directories
    os.makedirs('optimized_models', exist_ok=True)
    os.makedirs('results', exist_ok=True)
    
    # Find the latest trained model
    model_dir = 'models'
    model_files = [f for f in os.listdir(model_dir) if f.endswith('.h5')] if os.path.exists(model_dir) else []
    
    if not model_files:
        print("No trained models found. Using a new model.")
        model_path = None
    else:
        latest_model = max(model_files, key=lambda x: int(x.split('_')[-1].split('.')[0]) if x.split('_')[-1].split('.')[0].isdigit() else 0)
        model_path = os.path.join(model_dir, latest_model)
        print(f"Using latest trained model: {model_path}")
    
    # Define state shape (same as in training)
    grid_shape = (30, 30, 8)
    metadata_shape = (3,)
    
    # Create original model (same architecture as training)
    print("\nBenchmarking original model architecture...")
    original_model = tf.keras.models.Sequential([
        tf.keras.layers.Conv2D(32, (3, 3), activation='relu', input_shape=grid_shape),
        tf.keras.layers.Conv2D(64, (3, 3), activation='relu'),
        tf.keras.layers.Flatten(),
        tf.keras.layers.Dense(256, activation='relu'),
        tf.keras.layers.Dense(128, activation='relu'),
        tf.keras.layers.Dense(64, activation='relu'),
        tf.keras.layers.Dense(4, activation='linear')
    ])
    
    # Generate random test data
    grid_features = np.random.rand(1, 30, 30, 8).astype(np.float32)
    metadata = np.random.rand(1, 3).astype(np.float32)
    
    # Warm-up and benchmark original architecture
    for _ in range(5):
        _ = original_model(grid_features, training=False)
    
    original_times = []
    for _ in range(50):
        start_time = time.time()
        _ = original_model(grid_features, training=False)
        inference_time = (time.time() - start_time) * 1000
        original_times.append(inference_time)
    
    orig_avg = np.mean(original_times)
    orig_max = np.max(original_times)
    print(f"Original model: Avg={orig_avg:.2f}ms, Max={orig_max:.2f}ms")
    
    # Create optimized model
    print("\nBenchmarking optimized model...")
    optimized_model = SimpleOptimizedModel(
        state_shape=[(grid_shape), (metadata_shape)],
        model_path=model_path
    )
    
    # Benchmark optimized model
    opt_avg, opt_max, opt_min = optimized_model.benchmark_inference(num_tests=100)
    
    # Create and benchmark fallback heuristic
    print("\nBenchmarking fallback heuristic...")
    fallback = HeuristicFallback()
    
    fallback_times = []
    for _ in range(100):
        start_time = time.time()
        _ = fallback.predict([grid_features, metadata])
        inference_time = (time.time() - start_time) * 1000
        fallback_times.append(inference_time)
    
    fb_avg = np.mean(fallback_times)
    fb_max = np.max(fallback_times)
    fb_min = np.min(fallback_times)
    
    print(f"Fallback heuristic: Avg={fb_avg:.2f}ms, Min={fb_min:.2f}ms, Max={fb_max:.2f}ms")
    
    # Plot comparison
    labels = ['Original', 'Optimized', 'Fallback']
    avg_times = [orig_avg, opt_avg, fb_avg]
    max_times = [orig_max, opt_max, fb_max]
    
    plt.figure(figsize=(10, 6))
    x = np.arange(len(labels))
    width = 0.35
    
    plt.bar(x - width/2, avg_times, width, label='Avg Inference Time (ms)')
    plt.bar(x + width/2, max_times, width, label='Max Inference Time (ms)')
    
    plt.axhline(y=150, color='r', linestyle='--', label='150ms Constraint')
    
    plt.xlabel('Model Version')
    plt.ylabel('Time (ms)')
    plt.title('Inference Time Comparison')
    plt.xticks(x, labels)
    plt.legend()
    
    plt.savefig('results/inference_time_comparison.png')
    
    # Save the optimized model
    optimized_model.save('optimized_models/zooscape_optimized.h5')
    
    # Determine if optimization is sufficient
    if opt_max <= 150:
        print("\nOptimization successful! Optimized model meets the 150ms constraint.")
        return "optimized"
    elif fb_max <= 150:
        print("\nFallback heuristic meets the 150ms constraint and will be used as a safety mechanism.")
        return "fallback"
    else:
        print("\nWarning: Neither optimized model nor fallback meets the 150ms constraint consistently.")
        print("Further optimization or hardware improvements may be needed.")
        return "warning"

if __name__ == "__main__":
    benchmark_and_optimize()
