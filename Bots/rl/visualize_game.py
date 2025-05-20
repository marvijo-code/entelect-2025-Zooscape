import os
import sys
import time
import numpy as np
import matplotlib.pyplot as plt
import matplotlib.animation as animation
from matplotlib.colors import ListedColormap
import subprocess
import threading
import json
import socket
from datetime import datetime

# Create directories for visualization
os.makedirs('visualization', exist_ok=True)

class ZooscapeGameConnector:
    """
    Connects to the Zooscape game engine to receive real-time game state and scores
    """
    def __init__(self, host='localhost', port=5001):
        self.host = host
        self.port = port
        self.socket = None
        self.connected = False
        self.game_state = {}
        self.scores = {
            'tick': 0,
            'rl_score': 0,
            'ref_score': 0,
            'rl_captures': 0,
            'ref_captures': 0
        }
        
    def connect(self):
        """Connect to the game engine"""
        try:
            self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.socket.connect((self.host, self.port))
            self.connected = True
            print(f"Connected to game engine at {self.host}:{self.port}")
            return True
        except Exception as e:
            print(f"Failed to connect to game engine: {e}")
            return False
            
    def receive_data(self):
        """Receive and parse game state and score data"""
        if not self.connected:
            return False
            
        try:
            # Receive data from socket
            data = self.socket.recv(4096)
            if not data:
                return False
                
            # Parse JSON data
            game_data = json.loads(data.decode('utf-8'))
            
            # Update game state and scores
            if 'game_state' in game_data:
                self.game_state = game_data['game_state']
            if 'scores' in game_data:
                self.scores = game_data['scores']
                
            return True
        except Exception as e:
            print(f"Error receiving data: {e}")
            return False
            
    def close(self):
        """Close the connection"""
        if self.socket:
            self.socket.close()
            self.connected = False

class ZooscapeVisualizer:
    """
    Visualizer for Zooscape game showing RL bot vs Reference Bot with live scores
    """
    def __init__(self, connector=None):
        self.fig, self.axes = plt.subplots(1, 2, figsize=(15, 7))
        self.fig.suptitle('Zooscape: RL Bot vs Reference Bot', fontsize=16)
        
        # Game connector
        self.connector = connector
        if self.connector is None:
            # Create a dummy connector for simulation
            self.connector = type('DummyConnector', (), {
                'game_state': {},
                'scores': {
                    'tick': 0,
                    'rl_score': 0,
                    'ref_score': 0,
                    'rl_captures': 0,
                    'ref_captures': 0
                },
                'receive_data': lambda: True
            })
        
        # Game state
        self.grid_size = 30
        self.rl_bot_pos = (0, 0)
        self.ref_bot_pos = (0, 0)
        self.zookeeper_pos = (0, 0)
        self.pellets = []
        self.walls = []
        
        # Scores
        self.rl_bot_score = 0
        self.ref_bot_score = 0
        self.rl_captures = 0
        self.ref_captures = 0
        self.tick = 0
        
        # Score history for plotting
        self.ticks = []
        self.rl_scores = []
        self.ref_scores = []
        
        # Initialize plots
        self.init_grid_plot()
        self.init_score_plot()
        
        # Game process
        self.game_process = None
        self.stop_event = threading.Event()
        
    def init_grid_plot(self):
        """Initialize the grid visualization"""
        self.grid = np.zeros((self.grid_size, self.grid_size))
        
        # Create custom colormap
        colors = ['white', 'black', 'yellow', 'red', 'blue', 'green']
        self.cmap = ListedColormap(colors)
        
        # 0: empty, 1: wall, 2: pellet, 3: zookeeper, 4: RL bot, 5: Reference bot
        self.grid_plot = self.axes[0].imshow(
            self.grid, 
            cmap=self.cmap, 
            vmin=0, 
            vmax=5,
            interpolation='nearest'
        )
        
        self.axes[0].set_title('Game Grid')
        self.axes[0].set_xticks([])
        self.axes[0].set_yticks([])
        
        # Add legend
        from matplotlib.patches import Patch
        legend_elements = [
            Patch(facecolor='white', edgecolor='gray', label='Empty'),
            Patch(facecolor='black', label='Wall'),
            Patch(facecolor='yellow', label='Pellet'),
            Patch(facecolor='red', label='Zookeeper'),
            Patch(facecolor='blue', label='RL Bot'),
            Patch(facecolor='green', label='Reference Bot')
        ]
        self.axes[0].legend(handles=legend_elements, loc='upper center', 
                           bbox_to_anchor=(0.5, -0.05), ncol=3)
        
    def init_score_plot(self):
        """Initialize the score visualization"""
        self.score_plot, = self.axes[1].plot([], [], 'b-', label='RL Bot')
        self.ref_score_plot, = self.axes[1].plot([], [], 'g-', label='Reference Bot')
        
        self.axes[1].set_title('Score Progression')
        self.axes[1].set_xlabel('Game Ticks')
        self.axes[1].set_ylabel('Score')
        self.axes[1].legend()
        self.axes[1].grid(True)
        
        # Text annotations for current scores and stats
        self.score_text = self.axes[1].text(0.05, 0.95, '', transform=self.axes[1].transAxes,
                                           verticalalignment='top', fontsize=10)
        
    def update_grid(self, game_state):
        """Update grid based on game state"""
        # Reset grid
        self.grid = np.zeros((self.grid_size, self.grid_size))
        
        # Add walls
        for wall in game_state.get('walls', []):
            x, y = wall
            if 0 <= x < self.grid_size and 0 <= y < self.grid_size:
                self.grid[y, x] = 1
                
        # Add pellets
        for pellet in game_state.get('pellets', []):
            x, y = pellet
            if 0 <= x < self.grid_size and 0 <= y < self.grid_size:
                self.grid[y, x] = 2
                
        # Add zookeeper
        zk_x, zk_y = game_state.get('zookeeper', (0, 0))
        if 0 <= zk_x < self.grid_size and 0 <= zk_y < self.grid_size:
            self.grid[zk_y, zk_x] = 3
            
        # Add RL bot
        rl_x, rl_y = game_state.get('rl_bot', (0, 0))
        if 0 <= rl_x < self.grid_size and 0 <= rl_y < self.grid_size:
            self.grid[rl_y, rl_x] = 4
            
        # Add Reference bot
        ref_x, ref_y = game_state.get('ref_bot', (0, 0))
        if 0 <= ref_x < self.grid_size and 0 <= ref_y < self.grid_size:
            self.grid[ref_y, ref_x] = 5
            
        # Update grid plot
        self.grid_plot.set_data(self.grid)
        
    def update_scores(self, scores):
        """Update scores and score plot"""
        self.tick = scores.get('tick', self.tick + 1)
        self.rl_bot_score = scores.get('rl_score', self.rl_bot_score)
        self.ref_bot_score = scores.get('ref_score', self.ref_bot_score)
        self.rl_captures = scores.get('rl_captures', self.rl_captures)
        self.ref_captures = scores.get('ref_captures', self.ref_captures)
        
        # Update history
        self.ticks.append(self.tick)
        self.rl_scores.append(self.rl_bot_score)
        self.ref_scores.append(self.ref_bot_score)
        
        # Update plots
        self.score_plot.set_data(self.ticks, self.rl_scores)
        self.ref_score_plot.set_data(self.ticks, self.ref_scores)
        
        # Adjust axes limits if needed
        self.axes[1].relim()
        self.axes[1].autoscale_view()
        
        # Update score text
        score_info = (
            f"Tick: {self.tick}\n"
            f"RL Bot Score: {self.rl_bot_score}\n"
            f"RL Bot Captures: {self.rl_captures}\n"
            f"Ref Bot Score: {self.ref_bot_score}\n"
            f"Ref Bot Captures: {self.ref_captures}\n"
        )
        self.score_text.set_text(score_info)
        
    def update(self, frame):
        """Animation update function"""
        # Get data from connector or simulate if not available
        if hasattr(self.connector, 'receive_data'):
            data_received = self.connector.receive_data()
            
            if data_received:
                game_state = self.connector.game_state
                scores = self.connector.scores
            else:
                # Simulate if no data received
                game_state, scores = self.simulate_game_data(frame)
        else:
            # Simulate if no connector
            game_state, scores = self.simulate_game_data(frame)
        
        self.update_grid(game_state)
        self.update_scores(scores)
        
        return [self.grid_plot, self.score_plot, self.ref_score_plot, self.score_text]
    
    def simulate_game_data(self, frame):
        """Simulate game data for visualization testing"""
        # Simulate game state
        game_state = {
            'walls': [(i, 0) for i in range(self.grid_size)] + 
                     [(i, self.grid_size-1) for i in range(self.grid_size)] +
                     [(0, i) for i in range(self.grid_size)] +
                     [(self.grid_size-1, i) for i in range(self.grid_size)],
            'pellets': [(np.random.randint(1, self.grid_size-1), 
                         np.random.randint(1, self.grid_size-1)) 
                        for _ in range(20)],
            'zookeeper': (np.random.randint(1, self.grid_size-1), 
                          np.random.randint(1, self.grid_size-1)),
            'rl_bot': (np.random.randint(1, self.grid_size-1), 
                       np.random.randint(1, self.grid_size-1)),
            'ref_bot': (np.random.randint(1, self.grid_size-1), 
                        np.random.randint(1, self.grid_size-1))
        }
        
        # Simulate scores
        scores = {
            'tick': frame + 1,
            'rl_score': self.rl_bot_score + np.random.randint(0, 2),
            'ref_score': self.ref_bot_score + np.random.randint(0, 2),
            'rl_captures': self.rl_captures + (1 if np.random.random() < 0.05 else 0),
            'ref_captures': self.ref_captures + (1 if np.random.random() < 0.05 else 0)
        }
        
        return game_state, scores
    
    def start_visualization(self):
        """Start the visualization"""
        # Create animation
        ani = animation.FuncAnimation(
            self.fig, 
            self.update, 
            frames=None,  # Run indefinitely
            interval=200,  # 200ms between frames
            blit=True
        )
        
        plt.tight_layout()
        plt.show()
        
    def save_visualization(self, filename='visualization/game_replay.gif', frames=100):
        """Save the visualization as a GIF file"""
        # Create animation
        ani = animation.FuncAnimation(
            self.fig, 
            self.update, 
            frames=frames,
            interval=200,
            blit=True
        )
        
        # Save animation as GIF (supported by Pillow without ffmpeg)
        if not filename.endswith('.gif'):
            filename = filename.replace('.mp4', '.gif')
        
        ani.save(filename, writer='pillow', fps=5)
        print(f"Visualization saved to {filename}")

class ZooscapeGameRunner:
    """
    Runs the Zooscape game with RL bot vs Reference bot and captures state for visualization
    """
    def __init__(self, rl_bot_path, ref_bot_path, game_engine_path):
        self.rl_bot_path = rl_bot_path
        self.ref_bot_path = ref_bot_path
        self.game_engine_path = game_engine_path
        self.connector = ZooscapeGameConnector()
        self.visualizer = ZooscapeVisualizer(self.connector)
        self.processes = []
        
    def start_game_engine(self):
        """Start the Zooscape game engine"""
        try:
            # Use the run.sh script to start the game engine
            cmd = f"cd {self.game_engine_path} && ./run.sh"
            process = subprocess.Popen(cmd, shell=True)
            self.processes.append(process)
            print("Game engine started")
            
            # Wait for the game engine to initialize
            time.sleep(5)
            return True
        except Exception as e:
            print(f"Failed to start game engine: {e}")
            return False
            
    def start_rl_bot(self):
        """Start the RL bot"""
        try:
            # Start the RL bot with the appropriate environment variables
            env = os.environ.copy()
            env["BOT_NICKNAME"] = "RLBot"
            cmd = f"cd {os.path.dirname(self.rl_bot_path)} && python3 {os.path.basename(self.rl_bot_path)}"
            process = subprocess.Popen(cmd, shell=True, env=env)
            self.processes.append(process)
            print("RL bot started")
            return True
        except Exception as e:
            print(f"Failed to start RL bot: {e}")
            return False
            
    def start_reference_bot(self):
        """Start the Reference bot"""
        try:
            # Start the Reference bot
            env = os.environ.copy()
            env["BOT_NICKNAME"] = "RefBot"
            cmd = f"cd {self.ref_bot_path} && dotnet run"
            process = subprocess.Popen(cmd, shell=True, env=env)
            self.processes.append(process)
            print("Reference bot started")
            return True
        except Exception as e:
            print(f"Failed to start Reference bot: {e}")
            return False
            
    def run_game(self):
        """Run the game and visualize it"""
        # Connect to the game engine
        if not self.connector.connect():
            print("Using simulation mode since connection failed")
            
        # Start the game components if not in simulation mode
        if self.connector.connected:
            self.start_game_engine()
            self.start_rl_bot()
            self.start_reference_bot()
        
        # Start the visualization
        self.visualizer.start_visualization()
        
    def run_and_save(self, filename='visualization/game_replay.mp4'):
        """Run the game and save the visualization"""
        # For saving, we'll use simulation mode
        self.visualizer.save_visualization(filename)
        
    def cleanup(self):
        """Clean up processes"""
        for process in self.processes:
            try:
                process.terminate()
            except:
                pass
        
        if self.connector.connected:
            self.connector.close()

# Example usage
if __name__ == "__main__":
    print("Starting Zooscape visualization...")
    
    # Set paths to components
    script_dir = os.path.dirname(os.path.abspath(__file__))
    rl_bot_path = os.path.join(script_dir, "RLBotProgram.cs")
    ref_bot_path = os.path.join(script_dir, "..", "ReferenceBot")
    game_engine_path = os.path.join(script_dir, "..")
    
    # Create and run visualizer
    runner = ZooscapeGameRunner(rl_bot_path, ref_bot_path, game_engine_path)
    
    try:
        # Choose whether to show live visualization or save to file
        if len(sys.argv) > 1 and sys.argv[1] == '--save':
            timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
            runner.run_and_save(f"visualization/game_replay_{timestamp}.mp4")
        else:
            runner.run_game()
    finally:
        runner.cleanup()
