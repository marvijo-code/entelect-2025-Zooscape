#!/usr/bin/env python3
"""
Test Trained Zooscape Model for Planning
Load a trained model and test it on game scenarios for move planning.
"""

import os
import json
import numpy as np
import glob

# Import the RL agent
from fixed_rl_agent import DQNAgent

class ZooscapeModelTester:
    def __init__(self, model_path, logs_dir="../../logs"):
        self.logs_dir = logs_dir
        self.model_path = model_path
        
        # Initialize agent with same parameters as training
        self.agent = DQNAgent(
            state_shape=((51, 51, 1), (4,)),  # Grid shape and metadata shape
            action_size=4,       # Up, Down, Left, Right
            learning_rate=0.001,
            epsilon=0.0,         # No exploration for testing
            epsilon_decay=0.995,
            epsilon_min=0.01
        )
        
        # Load the trained model
        self.load_model()
        
    def load_model(self):
        """Load the trained model"""
        try:
            success = self.agent.load(self.model_path)
            if success:
                print(f"✅ Successfully loaded model from {self.model_path}")
                return True
            else:
                print(f"❌ Failed to load model from {self.model_path}")
                return False
        except Exception as e:
            print(f"❌ Error loading model: {e}")
            return False
    
    def parse_game_state(self, game_data):
        """Convert real game JSON to our GameState format"""
        try:
            # Extract map data
            cells = game_data.get('Cells', [])
            animals = game_data.get('Animals', [])
            zookeepers = game_data.get('Zookeepers', [])
            tick = game_data.get('Tick', 0)
            
            # Create 51x51 map
            game_map = np.zeros((51, 51), dtype=int)
            for cell in cells:
                x, y = cell['X'], cell['Y']
                content = cell['Content']
                if 0 <= x < 51 and 0 <= y < 51:
                    game_map[x][y] = content
            
            # Find our agent (first viable animal)
            our_animal = None
            for animal in animals:
                if animal.get('IsViable', True):
                    our_animal = animal
                    break
            
            if our_animal is None and animals:
                our_animal = animals[0]
            
            if our_animal is None:
                return None
            
            # Create GameState-like object
            state = {
                'map': game_map,
                'player_x': our_animal.get('X', 25),
                'player_y': our_animal.get('Y', 25),
                'score': our_animal.get('Score', 0),
                'tick': tick,
                'animals': animals,
                'zookeepers': zookeepers
            }
            
            return state
            
        except Exception as e:
            print(f"Error parsing game state: {e}")
            return None
    
    def state_to_features(self, state):
        """Convert state to neural network input features"""
        if state is None:
            grid_features = np.zeros((51, 51, 1), dtype=np.float32)
            metadata = np.zeros(4, dtype=np.float32)
            return (grid_features, metadata)
        
        # Grid features - reshape to include channel dimension
        grid_features = np.expand_dims(state['map'].astype(np.float32) / 10.0, axis=-1)
        
        # Metadata features (position and score)
        metadata = np.array([
            state['player_x'] / 51.0,  # Normalized x position
            state['player_y'] / 51.0,  # Normalized y position
            state['score'] / 10000.0,  # Normalized score
            state['tick'] / 1000.0     # Normalized tick
        ], dtype=np.float32)
        
        return (grid_features, metadata)
    
    def get_action_name(self, action):
        """Convert action number to readable name"""
        action_names = {0: "Up", 1: "Right", 2: "Down", 3: "Left"}
        return action_names.get(action, "Unknown")
    
    def plan_move(self, game_state_json):
        """Plan the next move given a game state"""
        state = self.parse_game_state(game_state_json)
        if state is None:
            return None, "Invalid game state"
        
        # Convert to features
        features = self.state_to_features(state)
        
        # Get action from model
        action = self.agent.act(features, training=False)
        action_name = self.get_action_name(action)
        
        # Get all Q-values for analysis
        grid_features, metadata = features
        grid_features = np.expand_dims(grid_features, 0)
        metadata = np.expand_dims(metadata, 0)
        q_values = self.agent.model.predict([grid_features, metadata], verbose=0)[0]
        
        analysis = {
            'chosen_action': action_name,
            'chosen_action_id': int(action),
            'q_values': {
                'Up': float(q_values[0]),
                'Right': float(q_values[1]), 
                'Down': float(q_values[2]),
                'Left': float(q_values[3])
            },
            'confidence': float(np.max(q_values) - np.mean(q_values)),
            'player_position': (state['player_x'], state['player_y']),
            'current_score': state['score'],
            'game_tick': state['tick']
        }
        
        return action_name, analysis
    
    def test_on_sample_logs(self, num_tests=5):
        """Test the model on sample game states from logs"""
        print(f"\n🧪 Testing trained model on {num_tests} game scenarios...")
        
        # Get some log directories
        log_dirs = []
        if os.path.exists(self.logs_dir):
            for item in os.listdir(self.logs_dir):
                item_path = os.path.join(self.logs_dir, item)
                if os.path.isdir(item_path) and item.startswith('202'):
                    log_dirs.append(item_path)
        
        if not log_dirs:
            print("No log directories found!")
            return
        
        tested = 0
        for log_dir in log_dirs[:3]:  # Test on first 3 directories
            json_files = glob.glob(os.path.join(log_dir, "*.json"))
            if not json_files:
                continue
                
            # Test on a few random states from this game
            test_files = json_files[::len(json_files) // 3][:2]  # Sample a couple states
            
            for json_file in test_files:
                if tested >= num_tests:
                    break
                    
                try:
                    with open(json_file, 'r', encoding='utf-8') as f:
                        game_data = json.load(f)
                    
                    action, analysis = self.plan_move(game_data)
                    
                    print(f"\n📍 Test {tested + 1} - File: {os.path.basename(json_file)}")
                    print(f"   Position: ({analysis['player_position'][0]}, {analysis['player_position'][1]})")
                    print(f"   Score: {analysis['current_score']:,}")
                    print(f"   Tick: {analysis['game_tick']}")
                    print(f"   🎯 Planned Action: {action}")
                    print(f"   📊 Q-Values: Up:{analysis['q_values']['Up']:.2f}, Right:{analysis['q_values']['Right']:.2f}, Down:{analysis['q_values']['Down']:.2f}, Left:{analysis['q_values']['Left']:.2f}")
                    print(f"   🔍 Confidence: {analysis['confidence']:.2f}")
                    
                    tested += 1
                    
                except Exception as e:
                    print(f"Error testing {json_file}: {e}")
                    continue
            
            if tested >= num_tests:
                break
        
        print(f"\n✅ Completed {tested} test scenarios")

def main():
    # Find the most recent model
    model_files = glob.glob("models/zooscape_real_logs_*.weights.h5")
    if not model_files:
        print("❌ No trained model files found! Run training first.")
        return
    
    # Use the most recent model
    latest_model = max(model_files, key=os.path.getctime)
    
    print("🤖 Zooscape Trained Model Tester")
    print(f"📁 Using model: {latest_model}")
    
    # Initialize tester
    tester = ZooscapeModelTester(latest_model)
    
    # Test on sample scenarios
    tester.test_on_sample_logs(num_tests=5)
    
    print("\n🎯 Model is ready for planning! You can use tester.plan_move(game_state_json) for real-time planning.")

if __name__ == "__main__":
    main() 