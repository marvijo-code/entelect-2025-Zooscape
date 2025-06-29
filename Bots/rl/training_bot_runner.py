import os
import sys
import time
import json
from signalr_client import SignalRClient
from fixed_rl_agent import RLBotService, CellContent

class TrainingBotRunner:
    def __init__(self, hub_url, bot_id="RLBot"):
        self.hub_url = hub_url
        self.bot_id = bot_id
        self.client = SignalRClient(hub_url)
        self.rl_service = RLBotService()
        self.is_registered = False

        # Register hub methods
        self.client.on("ReceiveGameState", self.on_receive_game_state)
        self.client.on("Registered", self.on_registered)
        self.client.on("ReceiveLastAction", lambda: print("Last action received."))


    def start(self):
        print(f"Starting bot {self.bot_id} and connecting to {self.hub_url}...")
        self.client.start()
        
        # Wait for the connection to be established before registering
        while not self.client.is_connected:
            time.sleep(0.1)
        
        print("Connection established. Registering bot...")
        self.client.send("Register", [self.bot_id])

        # Keep the script running
        try:
            while True:
                time.sleep(1)
        except KeyboardInterrupt:
            self.stop()

    def stop(self):
        print("Stopping bot...")
        if self.client:
            self.client.stop()

    def on_registered(self, bot_id):
        print(f"Bot successfully registered with ID: {bot_id}")
        self.is_registered = True

    def on_receive_game_state(self, game_state_json):
        if not self.is_registered:
            print("Received game state before being registered. Ignoring.")
            return

        try:
            # The game state is received as a JSON string, so we parse it.
            game_state_data = json.loads(game_state_json)
            
            # We need to transform this data into the mock objects our RL agent expects.
            game_state = self.create_mock_game_state(game_state_data)
            
            # Get the next action from our RL service
            action = self.rl_service.get_next_action(game_state)
            
            # The action from the RL service is an enum member, get its value
            action_value = action.value
            
            # Send the action back to the engine
            self.client.send("SendAction", [self.bot_id, action_value])

        except Exception as e:
            print(f"An error occurred while processing game state: {e}")
            # Optionally, send a default action to avoid timeout
            self.client.send("SendAction", [self.bot_id, 1]) # Default to "Up"

    def create_mock_game_state(self, data):
        """Creates a mock game state object from dictionary data."""
        return MockGameState(data)

# Mock classes to match the structure expected by fixed_rl_agent.py
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
        self.Score = data.get('Score', 0)  # Add Score field for reward calculation
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

if __name__ == "__main__":
    hub_url = "ws://localhost:5000/bothub"
    bot_id = "RLBot"
    
    if len(sys.argv) > 1:
        hub_url = sys.argv[1]
    if len(sys.argv) > 2:
        bot_id = sys.argv[2]

    runner = TrainingBotRunner(hub_url, bot_id)
    runner.start() 