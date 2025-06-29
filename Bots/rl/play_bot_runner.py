#!/usr/bin/env python3
"""
Zooscape Play Bot Runner
Uses a trained model to play the game (no training/learning)
"""

import sys
import json
import logging
import glob
import os
from signalrcore.hub_connection_builder import HubConnectionBuilder
from fixed_rl_agent import DQNAgent, CellContent
import numpy as np

# Set up logging
logging.basicConfig(level=logging.INFO)

class PlayBotService:
    """Bot service that uses trained model for playing (no training)"""
    
    def __init__(self, model_path=None):
        # Initialize DQN agent for inference only
        self.agent = DQNAgent(
            state_shape=((51, 51, 1), (4,)),  # Grid shape and metadata shape
            action_size=4,       # Up, Down, Left, Right
            learning_rate=0.001,
            epsilon=0.0,         # No exploration - pure exploitation
            epsilon_decay=0.995,
            epsilon_min=0.01
        )
        
        # Load trained model
        if model_path is None:
            model_path = self.find_latest_model()
        
        if model_path and os.path.exists(model_path):
            success = self.agent.load(model_path)
            if success:
                print(f"✅ Successfully loaded trained model: {model_path}")
            else:
                print(f"❌ Failed to load model: {model_path}")
        else:
            print("⚠️ No trained model found! Bot will use random actions.")
    
    def find_latest_model(self):
        """Find the most recent trained model"""
        model_files = glob.glob("models/zooscape_real_logs_*.weights.h5")
        if model_files:
            return max(model_files, key=os.path.getctime)
        return None
    
    def get_next_action(self, game_state):
        """Get the next action from the trained model"""
        try:
            # Convert game state to features
            features = self.game_state_to_features(game_state)
            
            # Get action from trained model (no training)
            action = self.agent.act(features, training=False)
            
            # Convert to action enum
            from fixed_rl_agent import Action
            actions = [Action.UP, Action.RIGHT, Action.DOWN, Action.LEFT]
            return actions[action] if action < len(actions) else Action.UP
            
        except Exception as e:
            print(f"Error getting action: {e}")
            # Return default action on error
            from fixed_rl_agent import Action
            return Action.UP
    
    def game_state_to_features(self, game_state):
        """Convert game state to neural network input features"""
        try:
            # Create 51x51 map from cells
            game_map = np.zeros((51, 51), dtype=int)
            for cell in game_state.Cells:
                if 0 <= cell.X < 51 and 0 <= cell.Y < 51:
                    game_map[cell.X][cell.Y] = cell.Content.value
            
            # Find our animal
            our_animal = None
            for animal in game_state.Animals:
                our_animal = animal
                break  # Use first animal for now
            
            if our_animal is None:
                # Default position if no animal found
                player_x, player_y, score = 25, 25, 0
            else:
                player_x, player_y, score = our_animal.X, our_animal.Y, our_animal.Score
            
            # Grid features - normalize and add channel dimension
            grid_features = np.expand_dims(game_map.astype(np.float32) / 10.0, axis=-1)
            
            # Metadata features (position and score)
            metadata = np.array([
                player_x / 51.0,        # Normalized x position
                player_y / 51.0,        # Normalized y position
                score / 10000.0,        # Normalized score
                game_state.Tick / 1000.0  # Normalized tick
            ], dtype=np.float32)
            
            return (grid_features, metadata)
            
        except Exception as e:
            print(f"Error converting game state to features: {e}")
            # Return default features
            grid_features = np.zeros((51, 51, 1), dtype=np.float32)
            metadata = np.zeros(4, dtype=np.float32)
            return (grid_features, metadata)

class PlayBotSignalR:
    """Play bot that uses trained model via SignalR"""
    
    def __init__(self, bot_id="PlayBot", model_path=None):
        self.bot_id = bot_id
        self.play_service = PlayBotService(model_path)
        self.connection = None
        self.connected = False
        print(f"🤖 Initialized Play Bot with ID: {self.bot_id}")

    def connect(self, hub_url="http://localhost:5000/bothub"):
        """Connect to the SignalR hub"""
        try:
            # Build the connection
            self.connection = HubConnectionBuilder() \
                .with_url(hub_url) \
                .configure_logging(logging.INFO) \
                .with_automatic_reconnect({
                    "type": "raw",
                    "keep_alive_interval": 10,
                    "reconnect_interval": 5,
                    "max_attempts": 5
                }).build()
            
            # Set up event handlers
            self.connection.on_open(self._on_connect)
            self.connection.on_close(self._on_disconnect)
            self.connection.on_error(self._on_error)
            
            # Set up game state handler
            self.connection.on("ReceiveGameState", self._on_receive_game_state)
            self.connection.on("Registered", self._on_registered)
            
            # Start the connection
            self.connection.start()
            print(f"🔗 Connected to SignalR hub: {hub_url}")
            
            # Register the bot
            self.connection.send("Register", [self.bot_id])
            print(f"📝 Registration message sent for bot: {self.bot_id}")
            
            self.connected = True
            return True
            
        except Exception as e:
            print(f"❌ Connection error: {e}")
            return False

    def _on_connect(self):
        """Called when connection is established"""
        print("✅ Connected to SignalR hub")
        self.connected = True

    def _on_disconnect(self):
        """Called when disconnected"""
        print("❌ Disconnected from SignalR hub")
        self.connected = False

    def _on_error(self, data):
        """Called when an error occurs"""
        print(f"⚠️ SignalR error: {data}")

    def _on_registered(self, bot_id):
        """Called when bot registration is confirmed"""
        print(f"🎯 Bot successfully registered: {bot_id}")

    def _on_receive_game_state(self, game_state_json):
        """Called when game state is received"""
        try:
            # Parse the JSON game state
            if isinstance(game_state_json, str):
                game_state_data = json.loads(game_state_json)
            else:
                game_state_data = game_state_json
            
            # Transform into mock objects for RL agent
            game_state = self.create_mock_game_state(game_state_data)
            
            # Get action from trained model
            action = self.play_service.get_next_action(game_state)
            
            # Send action back to server
            self.connection.send("SendAction", [self.bot_id, action.value])
            
        except Exception as e:
            print(f"❌ Error processing game state: {e}")
            # Send default action on error
            try:
                self.connection.send("SendAction", [self.bot_id, 1])
            except Exception:
                pass

    def create_mock_game_state(self, data):
        """Creates a mock game state object from dictionary data."""
        return MockGameState(data)

    def run(self):
        """Run the bot indefinitely"""
        try:
            print("🎮 Play bot is running... Press Ctrl+C to stop")
            # Keep the connection alive
            while self.connected:
                import time
                time.sleep(1)
        except KeyboardInterrupt:
            print("🛑 Bot stopped by user")
        finally:
            if self.connection:
                self.connection.stop()

# Mock classes to match the structure expected by the agent
class MockGameState:
    def __init__(self, data):
        self.TimeStamp = data.get('TimeStamp', '')
        self.Tick = data.get('Tick', 0)
        self.Cells = [MockCell(cell) for cell in data.get('Cells', [])]
        self.Animals = [MockAnimal(animal) for animal in data.get('Animals', [])]
        self.Zookeepers = [MockZookeeper(zk) for zk in data.get('Zookeepers', [])]
        self.Map = MockMap(data.get('Cells', []))

class MockCell:
    def __init__(self, data):
        self.X = data.get('X', 0)
        self.Y = data.get('Y', 0)
        self.Content = CellContent(data.get('Content', 0))

class MockAnimal:
    def __init__(self, data):
        self.Id = data.get('Id', '')
        self.X = data.get('X', 0)
        self.Y = data.get('Y', 0)
        self.BotId = data.get('BotId', '')
        self.Score = data.get('Score', 0)
        self.Position = MockPosition(self.X, self.Y)

class MockZookeeper:
    def __init__(self, data):
        self.Id = data.get('Id', '')
        self.X = data.get('X', 0)
        self.Y = data.get('Y', 0)
        self.Position = MockPosition(self.X, self.Y)

class MockPosition:
    def __init__(self, x, y):
        self.X = x
        self.Y = y

class MockMap:
    def __init__(self, cells_data):
        self.cells_1d = [MockCell(c) for c in cells_data]
        
        if not cells_data:
            self.Width = 0
            self.Height = 0
            return
            
        self.Width = max(c['X'] for c in cells_data) + 1
        self.Height = max(c['Y'] for c in cells_data) + 1
    
    @property 
    def Cells(self):
        """Return flat 1D array of cells."""
        return self.cells_1d

def main():
    """Main function to run the play bot"""
    hub_url = "http://localhost:5000/bothub"
    bot_id = "PlayBot"
    model_path = None
    
    if len(sys.argv) > 1:
        hub_url = sys.argv[1]
    if len(sys.argv) > 2:
        bot_id = sys.argv[2]
    if len(sys.argv) > 3:
        model_path = sys.argv[3]

    print("🚀 Starting Zooscape Play Bot")
    print(f"🌐 Hub URL: {hub_url}")
    print(f"🤖 Bot ID: {bot_id}")
    if model_path:
        print(f"🧠 Model: {model_path}")

    # Create and run the bot
    bot = PlayBotSignalR(bot_id, model_path)
    
    if bot.connect(hub_url):
        print("✅ Connected successfully. Running play bot...")
        bot.run()
    else:
        print("❌ Failed to connect to SignalR hub")
        return 1
    
    return 0

if __name__ == "__main__":
    sys.exit(main()) 