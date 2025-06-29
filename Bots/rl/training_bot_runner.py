import asyncio
import sys
import json
from signalr_async.netcore import Hub, Client
from fixed_rl_agent import RLBotService, CellContent

class RLBotHub(Hub):
    def __init__(self, bot_id="RLBot"):
        super().__init__("bothub")  # SignalR hub name
        self.bot_id = bot_id
        self.rl_service = RLBotService()
        print(f"Initialized RL Bot Hub with ID: {self.bot_id}")

    async def on_connect(self, connection_id: str) -> None:
        """Called when connection is established"""
        print(f"Connected to SignalR hub with connection ID: {connection_id}")
        # Register the bot
        await self.invoke("Register", self.bot_id)
        print(f"Registration message sent for bot: {self.bot_id}")

    async def on_disconnect(self) -> None:
        """Called when disconnected"""
        print("Disconnected from SignalR hub")

    def on_registered(self, bot_id: str) -> None:
        """Called when bot registration is confirmed"""
        print(f"Bot successfully registered: {bot_id}")

    def on_receive_game_state(self, game_state_json: str) -> None:
        """Called when game state is received - sync method"""
        try:
            # Parse the JSON game state
            game_state_data = json.loads(game_state_json)
            
            # Transform into mock objects for RL agent
            game_state = self.create_mock_game_state(game_state_data)
            
            # Get action from RL agent
            action = self.rl_service.get_next_action(game_state)
            
            # Send action back to server (create async task)
            asyncio.create_task(self.invoke("SendAction", self.bot_id, action.value))
            
        except Exception as e:
            print(f"Error processing game state: {e}")
            # Send default action on error
            asyncio.create_task(self.invoke("SendAction", self.bot_id, 1))

    def on_receive_last_action(self) -> None:
        """Called when last action is received"""
        pass  # We don't need to handle this

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

async def main():
    """Main async function to run the RL bot"""
    hub_url = "http://localhost:5000"
    bot_id = "RLBot"
    
    if len(sys.argv) > 1:
        hub_url = sys.argv[1].replace("ws://", "http://").replace("wss://", "https://")
    if len(sys.argv) > 2:
        bot_id = sys.argv[2]

    print(f"Connecting to: {hub_url}")
    print(f"Bot ID: {bot_id}")

    # Create the hub
    hub = RLBotHub(bot_id)
    
    try:
        # Create client and connect
        async with Client(hub_url, hub) as client:
            print("Connected to SignalR. Waiting for game states...")
            # Keep running indefinitely
            while True:
                await asyncio.sleep(1)
    except KeyboardInterrupt:
        print("Bot stopped by user")
    except Exception as e:
        print(f"Connection error: {e}")

if __name__ == "__main__":
    # Run the async main function
    asyncio.run(main()) 