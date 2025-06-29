#!/usr/bin/env python3
"""
Pytest tests for the RL bot
"""

import pytest
import json
import os
import sys
from pathlib import Path

# Add the current directory to path for imports
sys.path.insert(0, str(Path(__file__).parent))

try:
    from fixed_rl_agent import RLBotService
except ImportError:
    # Fallback for testing
    from rl_agent import RLBotService

class MockGameState:
    """Mock game state class for testing"""
    def __init__(self, data):
        self.TimeStamp = data.get('TimeStamp', '')
        self.Tick = data.get('Tick', 0)
        self.Cells = [MockCell(cell) for cell in data.get('Cells', [])]
        self.Animals = [MockAnimal(animal) for animal in data.get('Animals', [])]
        self.Zookeepers = [MockZookeeper(zk) for zk in data.get('Zookeepers', [])]
        
        # Create a mock Map object for compatibility with the RL agent
        self.Map = MockMap(data.get('Cells', []))

class MockCell:
    """Mock cell class for testing"""
    def __init__(self, data):
        self.X = data.get('X', 0)
        self.Y = data.get('Y', 0)
        self.Content = data.get('Content', 0)

class MockAnimal:
    """Mock animal class for testing"""
    def __init__(self, data):
        self.Id = data.get('Id', '')
        self.X = data.get('X', 0)
        self.Y = data.get('Y', 0)
        self.Score = data.get('Score', 0)
        self.CapturedCounter = data.get('CapturedCounter', 0)
        self.Nickname = data.get('Nickname', '')
        self.BotId = "RLBot"  # Set to what the RL agent expects
        self.Position = MockPosition(self.X, self.Y)

class MockZookeeper:
    """Mock zookeeper class for testing"""
    def __init__(self, data):
        self.Id = data.get('Id', '')
        self.X = data.get('X', 0)
        self.Y = data.get('Y', 0)
        self.Position = MockPosition(self.X, self.Y)

class MockPosition:
    """Mock position class for testing"""
    def __init__(self, x, y):
        self.X = x
        self.Y = y

class MockMap:
    """Mock map class for testing"""
    def __init__(self, cells_data):
        self.cells_2d = {}
        max_x = 0
        max_y = 0
        
        # Convert flat cell list to 2D structure
        for cell_data in cells_data:
            x = cell_data.get('X', 0)
            y = cell_data.get('Y', 0)
            max_x = max(max_x, x)
            max_y = max(max_y, y)
            
            if y not in self.cells_2d:
                self.cells_2d[y] = {}
            self.cells_2d[y][x] = MockCell(cell_data)
        
        self.Width = max_x + 1
        self.Height = max_y + 1
    
    @property 
    def Cells(self):
        """Return flat 1D array of cells for compatibility with RL agent"""
        result = []
        for y in range(self.Height):
            for x in range(self.Width):
                if y in self.cells_2d and x in self.cells_2d[y]:
                    result.append(self.cells_2d[y][x])
                else:
                    # Create empty cell if not found
                    result.append(MockCell({'X': x, 'Y': y, 'Content': 0}))
        return result

def load_game_state(filename):
    """Load a game state from JSON file"""
    # Look for the file in FunctionalTests/GameStates directory
    test_file_paths = [
        f"../../FunctionalTests/GameStates/{filename}",
        f"../FunctionalTests/GameStates/{filename}",
        f"FunctionalTests/GameStates/{filename}",
        filename
    ]
    
    for path in test_file_paths:
        if os.path.exists(path):
            with open(path, 'r') as f:
                data = json.load(f)
                return MockGameState(data)
    
    raise FileNotFoundError(f"Could not find game state file: {filename}")

@pytest.fixture
def rl_bot():
    """Create an RL bot instance for testing"""
    # Try to load a trained model if available
    model_path = os.path.join(os.path.dirname(__file__), 'models', 'best_model.h5')
    if os.path.exists(model_path):
        return RLBotService(model_path=model_path)
    else:
        return RLBotService()

@pytest.fixture
def game_state_162():
    """Load the 162.json game state"""
    return load_game_state("162.json")

class TestRLBot:
    """Test cases for the RL bot"""
    
    def test_rl_bot_initialization(self, rl_bot):
        """Test that the RL bot initializes correctly"""
        assert rl_bot is not None
        assert hasattr(rl_bot, 'get_next_action') or hasattr(rl_bot, 'get_action') or hasattr(rl_bot, 'ProcessState')
    
    def test_rl_bot_with_162_gamestate(self, rl_bot, game_state_162):
        """Test RL bot action on game state 162"""
        # Set a test bot ID
        test_bot_id = "RLBot"  # Use the ID the RL agent expects
        
        if hasattr(rl_bot, 'SetBotId'):
            rl_bot.SetBotId(test_bot_id)
        elif hasattr(rl_bot, 'bot_id'):
            rl_bot.bot_id = test_bot_id
        
        # Get action from the bot
        action = None
        if hasattr(rl_bot, 'get_next_action'):
            action = rl_bot.get_next_action(game_state_162)
        elif hasattr(rl_bot, 'get_action'):
            action = rl_bot.get_action(game_state_162)
        elif hasattr(rl_bot, 'ProcessState'):
            command = rl_bot.ProcessState(game_state_162)
            action = command.Action if hasattr(command, 'Action') else command
        
        # Validate action
        assert action is not None
        valid_actions = [1, 2, 3, 4, 5, "Up", "Down", "Left", "Right", "UseItem"]
        assert action in valid_actions or (isinstance(action, int) and 1 <= action <= 5)
        
        print(f"RL Bot chose action: {action} for game state 162")
    
    def test_rl_bot_returns_valid_actions(self, rl_bot, game_state_162):
        """Test that RL bot always returns valid actions"""
        test_bot_id = "test-bot-456"
        
        if hasattr(rl_bot, 'SetBotId'):
            rl_bot.SetBotId(test_bot_id)
        elif hasattr(rl_bot, 'bot_id'):
            rl_bot.bot_id = test_bot_id
        
        # Test multiple runs to ensure consistency
        actions = []
        for _ in range(5):
            if hasattr(rl_bot, 'get_next_action'):
                action = rl_bot.get_next_action(game_state_162)
            elif hasattr(rl_bot, 'get_action'):
                action = rl_bot.get_action(game_state_162)
            elif hasattr(rl_bot, 'ProcessState'):
                command = rl_bot.ProcessState(game_state_162)
                action = command.Action if hasattr(command, 'Action') else command
            
            actions.append(action)
            
            # Validate each action
            valid_actions = [1, 2, 3, 4, 5, "Up", "Down", "Left", "Right", "UseItem"]
            assert action in valid_actions or (isinstance(action, int) and 1 <= action <= 5)
        
        print(f"RL Bot actions over 5 runs: {actions}")
    
    def test_rl_bot_with_different_bot_ids(self, rl_bot, game_state_162):
        """Test RL bot with different bot IDs"""
        bot_ids = ["bot1", "bot2", "test-bot", "12345"]
        
        for bot_id in bot_ids:
            if hasattr(rl_bot, 'SetBotId'):
                rl_bot.SetBotId(bot_id)
            elif hasattr(rl_bot, 'bot_id'):
                rl_bot.bot_id = bot_id
            
            # Get action
            if hasattr(rl_bot, 'get_next_action'):
                action = rl_bot.get_next_action(game_state_162)
            elif hasattr(rl_bot, 'get_action'):
                action = rl_bot.get_action(game_state_162)
            elif hasattr(rl_bot, 'ProcessState'):
                command = rl_bot.ProcessState(game_state_162)
                action = command.Action if hasattr(command, 'Action') else command
            
            # Validate action
            valid_actions = [1, 2, 3, 4, 5, "Up", "Down", "Left", "Right", "UseItem"]
            assert action in valid_actions or (isinstance(action, int) and 1 <= action <= 5)
            
            print(f"Bot ID {bot_id} -> Action: {action}")

def test_game_state_162_loads():
    """Test that the 162.json game state loads correctly"""
    game_state = load_game_state("162.json")
    
    assert game_state is not None
    assert game_state.Tick == 162
    assert len(game_state.Cells) > 0
    assert len(game_state.Animals) >= 0  # May or may not have animals
    assert len(game_state.Zookeepers) >= 0  # May or may not have zookeepers
    
    print(f"Game state 162 loaded successfully:")
    print(f"  Tick: {game_state.Tick}")
    print(f"  Cells: {len(game_state.Cells)}")
    print(f"  Animals: {len(game_state.Animals)}")
    print(f"  Zookeepers: {len(game_state.Zookeepers)}")

if __name__ == "__main__":
    pytest.main([__file__, "-v", "-s"]) 