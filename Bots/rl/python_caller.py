#!/usr/bin/env python3
"""
Simple Python caller script that interfaces between C# and the RL agent
Usage: python python_caller.py <game_state_json_file> <bot_id>
"""

import sys
import json
import os
from typing import Dict, Any

# Add current directory to path so we can import our modules
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

try:
    from fixed_rl_agent import RLBotService
except ImportError as e:
    print(f"Failed to import RL agent: {e}", file=sys.stderr)
    sys.exit(1)

def convert_gamestate_format(cs_gamestate: Dict[str, Any]) -> Any:
    """
    Convert C# GameState JSON to format expected by Python RL agent
    """
    class GameStateObj:
        def __init__(self, data):
            self.TimeStamp = data.get('timeStamp', '')
            self.Tick = data.get('tick', 0)
            self.Cells = [CellObj(cell) for cell in data.get('cells', [])]
            self.Animals = [AnimalObj(animal) for animal in data.get('animals', [])]
            self.Zookeepers = [ZookeeperObj(zk) for zk in data.get('zookeepers', [])]
    
    class CellObj:
        def __init__(self, data):
            self.X = data.get('x', 0)
            self.Y = data.get('y', 0)
            self.Content = data.get('content', 0)
    
    class AnimalObj:
        def __init__(self, data):
            self.Id = data.get('id', '')
            self.X = data.get('x', 0)
            self.Y = data.get('y', 0)
            self.Score = data.get('score', 0)
            self.CapturedCounter = data.get('capturedCounter', 0)
            self.Nickname = data.get('nickname', '')
    
    class ZookeeperObj:
        def __init__(self, data):
            self.Id = data.get('id', '')
            self.X = data.get('x', 0)
            self.Y = data.get('y', 0)
    
    return GameStateObj(cs_gamestate)

def main():
    if len(sys.argv) != 3:
        print("Usage: python python_caller.py <game_state_json_file> <bot_id>", file=sys.stderr)
        sys.exit(1)
    
    json_file_path = sys.argv[1]
    bot_id = sys.argv[2]
    
    try:
        # Load game state from JSON file
        with open(json_file_path, 'r') as f:
            gamestate_data = json.load(f)
        
        # Convert to expected format
        game_state = convert_gamestate_format(gamestate_data)
        
        # Initialize RL bot (try to load existing model)
        model_path = os.path.join(os.path.dirname(__file__), 'models', 'best_model.h5')
        if os.path.exists(model_path):
            rl_bot = RLBotService(model_path=model_path)
        else:
            rl_bot = RLBotService()
        
        # Set bot ID
        if hasattr(rl_bot, 'SetBotId'):
            rl_bot.SetBotId(bot_id)
        elif hasattr(rl_bot, 'bot_id'):
            rl_bot.bot_id = bot_id
        
        # Get action
        if hasattr(rl_bot, 'get_next_action'):
            action = rl_bot.get_next_action(game_state)
        elif hasattr(rl_bot, 'get_action'):
            action = rl_bot.get_action(game_state)
        elif hasattr(rl_bot, 'ProcessState'):
            # For RLBotService from rl_agent.py
            command = rl_bot.ProcessState(game_state)
            action = command.Action if hasattr(command, 'Action') else command
        else:
            # Fallback
            action = 1  # Up (using C# BotAction enum value)
        
        # Convert action to string - using C# BotAction enum values
        action_names = {1: "Up", 2: "Down", 3: "Left", 4: "Right", 5: "UseItem"}
        if isinstance(action, int):
            action_str = action_names.get(action, "Up")
        else:
            action_str = str(action)
        
        # Output the action
        print(action_str)
        
    except Exception as e:
        print(f"Error in Python RL agent: {e}", file=sys.stderr)
        sys.exit(1)  # Fail since models should always be available

if __name__ == "__main__":
    main() 