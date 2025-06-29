#!/usr/bin/env python3
"""
Zooscape Offline Trainer - Real Game Data Version
Train RL models on actual game logs from the logs directory.
"""

import os
import json
import numpy as np
import matplotlib
matplotlib.use('Agg')  # Use non-interactive backend
import matplotlib.pyplot as plt
from collections import deque, defaultdict
import random
import glob
from datetime import datetime
import argparse

# Import the RL components from the current training system
from models.dqn_agent import DQNAgent
from models.base_agent import BaseRLAgent
from game_state import GameState
from action_space import ActionSpace

class ZooscapeOfflineTrainer:
    def __init__(self, logs_dir="../logs", model_dir="models", episodes_per_log_dir=10):
        self.logs_dir = logs_dir
        self.model_dir = model_dir
        self.episodes_per_log_dir = episodes_per_log_dir
        
        # Ensure model directory exists
        os.makedirs(model_dir, exist_ok=True)
        
        # Initialize RL agent (same as live training)
        self.agent = DQNAgent(
            state_size=51*51,  # Map size from real logs
            action_size=4,     # Up, Down, Left, Right
            learning_rate=0.001,
            epsilon=0.9,       # High exploration for training
            epsilon_decay=0.995,
            epsilon_min=0.01
        )
        
        # Training metrics
        self.episode_rewards = []
        self.episode_scores = []
        self.episode_lengths = []
        self.loss_history = []
        
    def load_game_logs(self):
        """Load all available game log directories"""
        log_dirs = []
        if os.path.exists(self.logs_dir):
            for item in os.listdir(self.logs_dir):
                item_path = os.path.join(self.logs_dir, item)
                if os.path.isdir(item_path) and item.startswith('202'):  # Date-based directories
                    log_dirs.append(item_path)
        
        log_dirs.sort()  # Process chronologically
        print(f"Found {len(log_dirs)} game log directories")
        return log_dirs
    
    def parse_game_state(self, game_data):
        """Convert real game JSON to our GameState format"""
        try:
            # Extract map data
            cells = game_data.get('Cells', [])
            animals = game_data.get('Animals', [])
            zookeepers = game_data.get('Zookeepers', [])
            tick = game_data.get('Tick', 0)
            
            # Create 51x51 map (based on real logs)
            game_map = np.zeros((51, 51), dtype=int)
            for cell in cells:
                x, y = cell['X'], cell['Y']
                content = cell['Content']
                if 0 <= x < 51 and 0 <= y < 51:
                    game_map[x][y] = content
            
            # Find our agent (first viable animal for simplicity)
            our_animal = None
            for animal in animals:
                if animal.get('IsViable', True):
                    our_animal = animal
                    break
            
            if our_animal is None and animals:
                our_animal = animals[0]  # Fallback to first animal
            
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
    
    def calculate_reward(self, prev_state, current_state, action):
        """Calculate reward based on state transition"""
        if prev_state is None or current_state is None:
            return 0
        
        reward = 0
        
        # Score improvement
        score_diff = current_state['score'] - prev_state['score']
        reward += score_diff * 0.01  # Scale down score rewards
        
        # Movement rewards/penalties
        prev_x, prev_y = prev_state['player_x'], prev_state['player_y']
        curr_x, curr_y = current_state['player_x'], current_state['player_y']
        
        # Encourage exploration
        if curr_x != prev_x or curr_y != prev_y:
            reward += 1  # Small movement reward
        
        # Penalty for staying in same place
        if curr_x == prev_x and curr_y == prev_y:
            reward -= 0.5
            
        # Check if moved into wall (content = 1)
        if 0 <= curr_x < 51 and 0 <= curr_y < 51:
            cell_content = current_state['map'][curr_x][curr_y]
            if cell_content == 1:  # Wall
                reward -= 5
            elif cell_content == 0:  # Empty space
                reward += 0.5
            elif cell_content >= 3:  # Animals/special objects
                reward += 10
        
        # Distance to nearest animal/special object
        animal_positions = [(a['X'], a['Y']) for a in current_state.get('animals', [])]
        if animal_positions:
            distances = [abs(curr_x - ax) + abs(curr_y - ay) for ax, ay in animal_positions]
            min_distance = min(distances)
            reward += max(0, 10 - min_distance) * 0.1  # Closer to animals = better
        
        return reward
    
    def state_to_vector(self, state):
        """Convert state to neural network input vector"""
        if state is None:
            return np.zeros(51*51)
        
        # Flatten the map
        map_vector = state['map'].flatten()
        
        # Normalize values
        map_vector = map_vector / 10.0  # Scale down content values
        
        return map_vector
    
    def process_game_sequence(self, json_files):
        """Process a sequence of game states from JSON files"""
        states = []
        rewards = []
        actions = []
        
        # Sort files by number
        json_files.sort(key=lambda x: int(os.path.basename(x).split('.')[0]))
        
        prev_state = None
        episode_reward = 0
        
        for i, json_file in enumerate(json_files):
            try:
                with open(json_file, 'r', encoding='utf-8') as f:
                    game_data = json.load(f)
                
                current_state = self.parse_game_state(game_data)
                if current_state is None:
                    continue
                
                # Calculate reward and action (simulate reasonable action)
                reward = self.calculate_reward(prev_state, current_state, 0)  # Action not known from logs
                
                # Simulate action based on movement
                action = 0  # Default action
                if prev_state is not None:
                    dx = current_state['player_x'] - prev_state['player_x']
                    dy = current_state['player_y'] - prev_state['player_y']
                    
                    if dx > 0:
                        action = 1  # Right
                    elif dx < 0:
                        action = 3  # Left
                    elif dy > 0:
                        action = 2  # Down
                    elif dy < 0:
                        action = 0  # Up
                
                if prev_state is not None:
                    # Store transition
                    state_vector = self.state_to_vector(prev_state)
                    next_state_vector = self.state_to_vector(current_state)
                    
                    # Add to agent's experience
                    self.agent.remember(state_vector, action, reward, next_state_vector, False)
                    episode_reward += reward
                
                prev_state = current_state
                
                # Train periodically
                if len(self.agent.memory) > 100 and i % 10 == 0:
                    loss = self.agent.replay(32)  # Batch size 32
                    if loss is not None:
                        self.loss_history.append(loss)
                
            except Exception as e:
                print(f"Error processing {json_file}: {e}")
                continue
        
        return episode_reward, len(json_files)
    
    def train_on_logs(self):
        """Train the model on all available game logs"""
        log_dirs = self.load_game_logs()
        
        if not log_dirs:
            print("No game log directories found!")
            return
        
        total_episodes = 0
        
        for log_dir in log_dirs:
            print(f"\nProcessing log directory: {os.path.basename(log_dir)}")
            
            # Get all JSON files in this directory
            json_files = glob.glob(os.path.join(log_dir, "*.json"))
            if not json_files:
                print(f"No JSON files found in {log_dir}")
                continue
            
            print(f"Found {len(json_files)} game states")
            
            # Process this game sequence multiple times for training
            for episode in range(self.episodes_per_log_dir):
                print(f"Episode {episode + 1}/{self.episodes_per_log_dir} for {os.path.basename(log_dir)}")
                
                # Shuffle files to create variation
                episode_files = json_files.copy()
                if episode > 0:  # Keep first episode in order
                    random.shuffle(episode_files)
                
                episode_reward, episode_length = self.process_game_sequence(episode_files)
                
                self.episode_rewards.append(episode_reward)
                self.episode_lengths.append(episode_length)
                
                # Extract final score if possible
                try:
                    with open(episode_files[-1], 'r', encoding='utf-8') as f:
                        final_data = json.load(f)
                    final_state = self.parse_game_state(final_data)
                    if final_state:
                        self.episode_scores.append(final_state['score'])
                    else:
                        self.episode_scores.append(0)
                except:
                    self.episode_scores.append(0)
                
                total_episodes += 1
                
                print(f"Episode reward: {episode_reward:.2f}, Length: {episode_length}, "
                      f"Epsilon: {self.agent.epsilon:.3f}")
        
        print(f"\nTraining completed! Total episodes: {total_episodes}")
        self.save_model_and_results()
    
    def save_model_and_results(self):
        """Save the trained model and training results"""
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        
        # Save model
        model_path = os.path.join(self.model_dir, f"zooscape_offline_model_{timestamp}.h5")
        self.agent.save_model(model_path)
        print(f"Model saved to: {model_path}")
        
        # Save training metrics
        metrics_path = os.path.join(self.model_dir, f"training_metrics_{timestamp}.json")
        metrics = {
            'episode_rewards': self.episode_rewards,
            'episode_scores': self.episode_scores,
            'episode_lengths': self.episode_lengths,
            'loss_history': self.loss_history,
            'final_epsilon': self.agent.epsilon,
            'total_episodes': len(self.episode_rewards)
        }
        
        with open(metrics_path, 'w') as f:
            json.dump(metrics, f, indent=2)
        print(f"Training metrics saved to: {metrics_path}")
        
        # Create plots
        self.create_training_plots(timestamp)
    
    def create_training_plots(self, timestamp):
        """Create training visualization plots"""
        plt.style.use('default')
        
        # Create subplots
        fig, axes = plt.subplots(2, 2, figsize=(15, 10))
        fig.suptitle(f'Zooscape Offline Training Results - {timestamp}', fontsize=16)
        
        # Episode rewards
        axes[0, 0].plot(self.episode_rewards)
        axes[0, 0].set_title('Episode Rewards')
        axes[0, 0].set_xlabel('Episode')
        axes[0, 0].set_ylabel('Reward')
        axes[0, 0].grid(True)
        
        # Episode scores
        axes[0, 1].plot(self.episode_scores)
        axes[0, 1].set_title('Episode Scores')
        axes[0, 1].set_xlabel('Episode')
        axes[0, 1].set_ylabel('Score')
        axes[0, 1].grid(True)
        
        # Episode lengths
        axes[1, 0].plot(self.episode_lengths)
        axes[1, 0].set_title('Episode Lengths')
        axes[1, 0].set_xlabel('Episode')
        axes[1, 0].set_ylabel('Length (Steps)')
        axes[1, 0].grid(True)
        
        # Loss history
        if self.loss_history:
            axes[1, 1].plot(self.loss_history)
            axes[1, 1].set_title('Training Loss')
            axes[1, 1].set_xlabel('Training Step')
            axes[1, 1].set_ylabel('Loss')
            axes[1, 1].grid(True)
        else:
            axes[1, 1].text(0.5, 0.5, 'No Loss Data', ha='center', va='center')
            axes[1, 1].set_title('Training Loss')
        
        plt.tight_layout()
        
        # Save plot
        plot_path = os.path.join(self.model_dir, f"training_plots_{timestamp}.png")
        plt.savefig(plot_path, dpi=300, bbox_inches='tight')
        plt.close()
        print(f"Training plots saved to: {plot_path}")
        
        # Create summary stats
        if self.episode_rewards:
            print(f"\nTraining Summary:")
            print(f"Average Reward: {np.mean(self.episode_rewards):.2f}")
            print(f"Max Reward: {np.max(self.episode_rewards):.2f}")
            print(f"Average Score: {np.mean(self.episode_scores):.2f}")
            print(f"Max Score: {np.max(self.episode_scores):.2f}")
            print(f"Final Epsilon: {self.agent.epsilon:.3f}")

def main():
    parser = argparse.ArgumentParser(description='Zooscape Offline Trainer')
    parser.add_argument('--logs_dir', default='../../logs', 
                      help='Directory containing game logs (default: ../../logs)')
    parser.add_argument('--model_dir', default='models', 
                      help='Directory to save models (default: models)')
    parser.add_argument('--episodes_per_log', type=int, default=5,
                      help='Episodes to train per log directory (default: 5)')
    
    args = parser.parse_args()
    
    print("Starting Zooscape Offline Training with Real Game Data")
    print(f"Logs directory: {args.logs_dir}")
    print(f"Model directory: {args.model_dir}")
    print(f"Episodes per log directory: {args.episodes_per_log}")
    
    trainer = ZooscapeOfflineTrainer(
        logs_dir=args.logs_dir,
        model_dir=args.model_dir,
        episodes_per_log_dir=args.episodes_per_log
    )
    
    trainer.train_on_logs()
    print("Training completed!")

if __name__ == "__main__":
    main() 