import os
import sys
import numpy as np
import tensorflow as tf
import time
import matplotlib.pyplot as plt
from fixed_rl_agent import RLBotService, StateProcessor, RewardCalculator, SimpleHeuristicFallback

# Create directories for results
os.makedirs('results', exist_ok=True)
os.makedirs('models', exist_ok=True)

def test_input_shapes():
    """
    Test input shape handling to ensure compatibility with the engine
    """
    print("Testing input shape handling...")
    
    # Create a mock game state
    game_state = create_mock_game_state()
    
    # Initialize state processor
    state_processor = StateProcessor()
    
    # Process state
    grid_features, metadata = state_processor.process_state(game_state)
    
    # Print shapes
    print(f"Grid features shape: {grid_features.shape}")
    print(f"Metadata shape: {metadata.shape}")
    
    # Initialize agent
    grid_shape = (30, 30, 8)
    metadata_shape = (3,)
    agent = RLBotService()
    
    # Test action selection
    action = agent.get_next_action(game_state)
    print(f"Selected action: {action}")
    
    # Test multiple consecutive actions to ensure memory handling
    print("\nTesting multiple consecutive actions...")
    for i in range(5):
        action = agent.get_next_action(game_state)
        print(f"Action {i+1}: {action}")
    
    print("\nInput shape handling test completed successfully!")

def test_training_loop():
    """
    Test the training loop to ensure experience replay works correctly
    """
    print("\nTesting training loop...")
    
    # Create a mock game state
    game_state = create_mock_game_state()
    
    # Initialize agent
    agent = RLBotService()
    
    # Run multiple steps to fill memory
    print("Filling replay memory...")
    for i in range(50):
        action = agent.get_next_action(game_state)
        # Modify game state slightly to simulate movement
        game_state = modify_mock_game_state(game_state, i)
    
    # Check memory size
    memory_size = len(agent.agent.memory)
    print(f"Replay memory size: {memory_size}")
    
    # Test replay
    print("Testing experience replay...")
    agent.agent.replay()
    
    print("Training loop test completed successfully!")

def test_model_save_load():
    """
    Test model saving and loading
    """
    print("\nTesting model save and load...")
    
    # Initialize agent
    agent = RLBotService()
    
    # Save model
    save_path = "models/test_model.weights.h5"
    agent.save_model(save_path)
    
    # Create a new agent and load the model
    new_agent = RLBotService(model_path=save_path)
    
    print("Model save and load test completed successfully!")

def test_inference_time():
    """
    Test inference time to ensure it meets the 150ms constraint
    """
    print("\nTesting inference time...")
    
    # Create a mock game state
    game_state = create_mock_game_state()
    
    # Initialize agent
    agent = RLBotService()
    
    # Warm up
    for _ in range(10):
        agent.get_next_action(game_state)
    
    # Test inference time
    times = []
    for i in range(100):
        start_time = time.time()
        agent.get_next_action(game_state)
        elapsed_time = (time.time() - start_time) * 1000
        times.append(elapsed_time)
        # Modify game state slightly to simulate movement
        game_state = modify_mock_game_state(game_state, i)
    
    # Calculate statistics
    avg_time = sum(times) / len(times)
    max_time = max(times)
    min_time = min(times)
    under_150ms = sum(1 for t in times if t < 150) / len(times) * 100
    
    print(f"Inference time (ms): Avg={avg_time:.2f}, Min={min_time:.2f}, Max={max_time:.2f}")
    print(f"Percentage of inferences under 150ms: {under_150ms:.2f}%")
    
    # Plot histogram
    plt.figure(figsize=(10, 6))
    plt.hist(times, bins=20, alpha=0.7, color='blue')
    plt.axvline(x=150, color='red', linestyle='--', label='150ms Constraint')
    plt.xlabel('Inference Time (ms)')
    plt.ylabel('Frequency')
    plt.title('Inference Time Distribution')
    plt.legend()
    plt.savefig('results/inference_time_histogram.png')
    
    print("Inference time test completed!")

def test_fallback_mechanism():
    """
    Test the fallback mechanism for emergency situations
    """
    print("\nTesting fallback mechanism...")
    
    # Create a mock game state
    game_state = create_mock_game_state()
    
    # Initialize fallback
    fallback = SimpleHeuristicFallback()
    
    # Test fallback action selection
    start_time = time.time()
    action = fallback.get_action(game_state)
    elapsed_time = (time.time() - start_time) * 1000
    
    print(f"Fallback action: {action}")
    print(f"Fallback inference time: {elapsed_time:.2f}ms")
    
    print("Fallback mechanism test completed successfully!")

def create_mock_game_state():
    """
    Create a mock game state for testing
    """
    # Create a simple class to mimic the game state structure
    class MockPosition:
        def __init__(self, x, y):
            self.X = x
            self.Y = y
    
    class MockCell:
        def __init__(self, content):
            self.Content = content
    
    class MockMap:
        def __init__(self, width, height):
            self.Width = width
            self.Height = height
            self.Cells = [[MockCell(0) for _ in range(width)] for _ in range(height)]
    
    class MockAnimal:
        def __init__(self, bot_id, x, y):
            self.BotId = bot_id
            self.Position = MockPosition(x, y)
    
    class MockZookeeper:
        def __init__(self, x, y):
            self.Position = MockPosition(x, y)
    
    class MockGameState:
        def __init__(self):
            self.Map = MockMap(30, 30)
            self.Animals = []
            self.Zookeepers = []
            self.Tick = 0
    
    # Create the mock game state
    game_state = MockGameState(            # Add walls around the edges
    for x in range(game_state.Map.Width):
        game_state.Map.Cells[0][x].Content = 1  # Wall
        game_state.Map.Cells[game_state.Map.Height-1][x].Content = 1  # Wall
    
    for y in range(game_state.Map.Height):
        game_state.Map.Cells[y][0].Content = 1  # Wall
        game_state.Map.Cells[y][game_state.Map.Width-1].Content = 1  # Wall
    
    # Add some pellets
    for i in range(50):
        x = np.random.randint(1, game_state.Map.Width-1)
        y = np.random.randint(1, game_state.Map.Height-1)
        game_state.Map.Cells[y][x].Content = 2  # Pellet    
    # Add animals
    game_state.Animals.append(MockAnimal("RLBot", 5, 5))
    game_state.Animals.append(MockAnimal("RefBot", 25, 25))
    
    # Add zookeepers
    game_state.Zookeepers.append(MockZookeeper(15, 15))
    game_state.Zookeepers.append(MockZookeeper(10, 20))
    
    # Set tick
    game_state.Tick = 100
    
    return game_state

def modify_mock_game_state(game_state, iteration):
    """
    Modify the mock game state to simulate movement
    """
    # Move RLBot
    for animal in game_state.Animals:
        if animal.BotId == "RLBot":
            # Move in a circle
            dx = int(np.cos(iteration / 10) * 3)
            dy = int(np.sin(iteration / 10) * 3)
            animal.Position.X = max(1, min(game_state.Map.Width-2, animal.Position.X + dx))
            animal.Position.Y = max(1, min(game_state.Map.Height-2, animal.Position.Y + dy))
    
    # Move zookeepers
    for i, zookeeper in enumerate(game_state.Zookeepers):
        # Move randomly
        dx = np.random.randint(-1, 2)
        dy = np.random.randint(-1, 2)
        zookeeper.Position.X = max(1, min(game_state.Map.Width-2, zookeeper.Position.X + dx))
        zookeeper.Position.Y = max(1, min(game_state.Map.Height-2, zookeeper.Position.Y + dy))
    
    # Update tick
    game_state.Tick += 1
    
    return game_state

if __name__ == "__main__":
    print("Starting RL bot integration tests...")
    
    # Run tests
    test_input_shapes()
    test_training_loop()
    test_model_save_load()
    test_inference_time()
    test_fallback_mechanism()
    
    print("\nAll tests completed successfully!")
