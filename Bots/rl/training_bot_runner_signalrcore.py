import sys
import json
import logging
from signalrcore.hub_connection_builder import HubConnectionBuilder
from fixed_rl_agent import RLBotService, CellContent

# Set up logging
logging.basicConfig(level=logging.INFO)

class RLBotSignalRCore:
    def __init__(self, bot_id="RLBot"):
        self.bot_id = bot_id
        self.rl_service = RLBotService()
        self.connection = None
        self.connected = False
        print("Initialized RL Bot with ID: {}".format(self.bot_id))

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
            
            # Set up game state handler (match C# bot event names)
            self.connection.on("GameState", self._on_receive_game_state)
            self.connection.on("Registered", self._on_registered)
            self.connection.on("Disconnect", self._on_disconnect_reason)
            
            # Start the connection
            self.connection.start()
            print("Connected to SignalR hub: {}".format(hub_url))
            
            # Register the bot with token and nickname (like C# bots do)
            import uuid
            token = str(uuid.uuid4())
            self.connection.send("Register", [token, self.bot_id])
            print("Registration message sent for bot: {} with token: {}".format(self.bot_id, token))
            
            self.connected = True
            return True
            
        except Exception as e:
            print("Connection error: {}".format(e))
            return False

    def _on_connect(self):
        """Called when connection is established"""
        print("Connected to SignalR hub")
        self.connected = True

    def _on_disconnect(self):
        """Called when disconnected"""
        print("Disconnected from SignalR hub")
        self.connected = False

    def _on_error(self, data):
        """Called when an error occurs"""
        print("SignalR error: {}".format(data))

    def _on_registered(self, bot_id):
        """Called when bot registration is confirmed"""
        print("Bot successfully registered with ID: {}".format(bot_id))
    
    def _on_disconnect_reason(self, reason):
        """Called when server sends disconnect"""
        print("Server disconnect reason: {}".format(reason))
        self.connected = False

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
            
            # Get action from RL agent
            action = self.rl_service.get_next_action(game_state)
            
            # Create BotCommand object and send (like C# bots do)
            bot_command = {
                "BotId": self.bot_id,
                "Action": action.value
            }
            self.connection.send("BotCommand", [bot_command])
            
        except Exception as e:
            print("Error processing game state: {}".format(e))
            # Send default action on error
            try:
                bot_command = {
                    "BotId": self.bot_id,
                    "Action": 1  # UP action
                }
                self.connection.send("BotCommand", [bot_command])
            except Exception:
                pass

    def create_mock_game_state(self, data):
        """Creates a mock game state object from dictionary data."""
        return MockGameState(data)

    def run(self):
        """Run the bot indefinitely"""
        try:
            # Keep the connection alive
            while self.connected:
                import time
                time.sleep(1)
        except KeyboardInterrupt:
            print("Bot stopped by user")
        finally:
            if self.connection:
                self.connection.stop()

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
    """Main function to run the RL bot"""
    hub_url = "http://localhost:5000/bothub"
    bot_id = "RLBot"
    
    if len(sys.argv) > 1:
        hub_url = sys.argv[1]
    if len(sys.argv) > 2:
        bot_id = sys.argv[2]

    print("Connecting to: {}".format(hub_url))
    print("Bot ID: {}".format(bot_id))

    # Create and run the bot
    bot = RLBotSignalRCore(bot_id)
    
    if bot.connect(hub_url):
        print("Connected successfully. Running bot...")
        bot.run()
    else:
        print("Failed to connect to SignalR hub")
        return 1
    
    return 0

if __name__ == "__main__":
    sys.exit(main()) 