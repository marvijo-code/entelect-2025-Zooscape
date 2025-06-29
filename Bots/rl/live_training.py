import os
import sys
import numpy as np
import tensorflow as tf
import time
import matplotlib.pyplot as plt
from collections import deque
import json
import subprocess
import threading
import signal

# Create directories for results
os.makedirs("results", exist_ok=True)
os.makedirs("models", exist_ok=True)
os.makedirs("logs", exist_ok=True)


class TrainingMonitor:
    """
    Monitors and visualizes the training progress of the RL bot
    """

    def __init__(self, log_file="logs/training_runner.log"):
        self.log_file = log_file
        self.rewards = []
        self.pellets = []
        self.captures = []
        self.inference_times = []
        self.epsilon_values = []
        self.games = []
        self.last_line_read = 0

    def parse_logs(self):
        """Parse the log file to extract training metrics"""
        if not os.path.exists(self.log_file):
            #print(f"Log file {self.log_file} not found.")
            return

        try:
            with open(self.log_file, "r", encoding='utf-8') as f:
                lines = f.readlines()
        except Exception as e:
            print(f"Error reading log file: {e}")
            return

        new_lines = lines[self.last_line_read:]
        self.last_line_read = len(lines)

        for line in new_lines:
            if "Tick:" in line and "Reward:" in line:
                try:
                    # Extract reward
                    reward_str = line.split("Reward:")[1].split(",")[0].strip()
                    reward = float(reward_str)
                    self.rewards.append(reward)

                    # Extract epsilon
                    if "Epsilon:" in line:
                        epsilon_str = line.split("Epsilon:")[1].split(",")[0].strip()
                        epsilon = float(epsilon_str)
                        self.epsilon_values.append(epsilon)

                    # Extract game number
                    if "Episode:" in line:
                        game_str = line.split("Episode:")[1].split(",")[0].strip()
                        game = int(game_str)
                        self.games.append(game)

                    # Extract inference time
                    if "Avg Inference:" in line:
                        avg_time_str = (
                            line.split("Avg Inference:")[1]
                            .split(",")[0]
                            .strip()
                            .replace("ms", "")
                        )
                        avg_time = float(avg_time_str)
                        self.inference_times.append(avg_time)
                except Exception as e:
                    print(f"Error parsing line: {line}")
                    print(f"Error: {e}")

            if "Game stats - Pellets:" in line:
                try:
                    # Extract pellets
                    pellets_str = line.split("Pellets:")[1].split(",")[0].strip()
                    pellets = int(pellets_str)
                    self.pellets.append(pellets)

                    # Extract captures
                    captures_str = line.split("Captures:")[1].strip()
                    captures = int(captures_str)
                    self.captures.append(captures)
                except Exception as e:
                    print(f"Error parsing line: {line}")
                    print(f"Error: {e}")

    def plot_training_progress(self, save_path="results/training_progress.png"):
        """Plot the training progress metrics"""
        if not self.rewards:
            self.parse_logs()

        if not self.rewards:
            print("No training data found yet.")
            return

        # Use non-interactive backend to avoid display issues
        plt.switch_backend('Agg')
        
        plt.figure(figsize=(15, 10))

        # Plot rewards
        plt.subplot(2, 2, 1)
        plt.plot(self.rewards)
        plt.title("Episode Rewards")
        plt.xlabel("Training Step")
        plt.ylabel("Reward")

        # Plot epsilon
        plt.subplot(2, 2, 2)
        if self.epsilon_values:
            plt.plot(self.epsilon_values)
        plt.title("Exploration Rate (Epsilon)")
        plt.xlabel("Training Step")
        plt.ylabel("Epsilon")

        # Plot pellets and captures
        if self.pellets:
            plt.subplot(2, 2, 3)
            plt.plot(self.pellets, label="Pellets")
            plt.plot(self.captures, label="Captures")
            plt.title("Pellets Collected vs Captures")
            plt.xlabel("Game")
            plt.ylabel("Count")
            plt.legend()

        # Plot inference times
        plt.subplot(2, 2, 4)
        if self.inference_times:
            plt.plot(self.inference_times)
        plt.axhline(y=150, color="r", linestyle="--", label="150ms Limit")
        plt.title("Average Inference Time")
        plt.xlabel("Training Step")
        plt.ylabel("Time (ms)")
        plt.legend()

        plt.tight_layout()
        try:
            plt.savefig(save_path)
            print("Training progress plot saved to {}".format(save_path))
        except Exception as e:
            print("Error saving plot: {}".format(e))
        finally:
            plt.close()

def start_engine_and_bots(log_file):
    """Start the Zooscape engine and both bots in separate processes"""
    
    # --- Start Engine ---
    engine_path = os.path.join("..", "..", "engine", "Zooscape")
    engine_cmd = "dotnet run --project {}".format(engine_path)
    print("Starting Engine: {}".format(engine_cmd))
    
    os.makedirs("logs", exist_ok=True)
    engine_log = open(os.path.join("logs", "engine.log"), "w", encoding='utf-8')
    
    try:
        engine_process = subprocess.Popen(
            engine_cmd, 
            shell=True, 
            stdout=engine_log, 
            stderr=subprocess.STDOUT,
            cwd=os.path.join("..", "..", "engine", "Zooscape"),
            creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if os.name == 'nt' else 0
        )
        print("Started Zooscape engine process.")
    except Exception as e:
        print("Error starting engine: {}".format(e))
        engine_log.close()
        return None, None, None

    # Wait for the engine to initialize
    print("Waiting for engine to start...")
    time.sleep(15)

    # --- Start RL Bot ---
    # Use the current Python interpreter (which should be in the venv)
    rl_bot_cmd = "python training_bot_runner_signalrcore.py"
    print("Starting RL Bot: {}".format(rl_bot_cmd))
    
    rl_log = open(log_file, "w", encoding='utf-8')
    
    try:
        rl_bot_process = subprocess.Popen(
            rl_bot_cmd, 
            shell=True, 
            stdout=rl_log, 
            stderr=subprocess.STDOUT,
            creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if os.name == 'nt' else 0
        )
        print("Started RL bot process.")
    except Exception as e:
        print("Error starting RL bot: {}".format(e))
        rl_log.close()
        return engine_process, None, None

    # --- Start Reference Bot ---
    ref_bot_path = os.path.join("..", "..", "engine", "ReferenceBot")
    ref_bot_cmd = "dotnet run --project {}".format(ref_bot_path)
    print("Starting Reference Bot: {}".format(ref_bot_cmd))
    
    ref_log = open(os.path.join("logs", "refbot.log"), "w", encoding='utf-8')
    
    try:
        ref_bot_process = subprocess.Popen(
            ref_bot_cmd, 
            shell=True, 
            stdout=ref_log, 
            stderr=subprocess.STDOUT,
            cwd=os.path.join("..", "..", "engine", "ReferenceBot"),
            creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if os.name == 'nt' else 0
        )
        print("Started Reference bot process.")
    except Exception as e:
        print("Error starting reference bot: {}".format(e))
        ref_log.close()
        return engine_process, rl_bot_process, None

    return engine_process, rl_bot_process, ref_bot_process

def terminate_processes(processes):
    """Safely terminate processes"""
    for process_name, process in processes:
        if process and process.poll() is None:
            try:
                print("Terminating {}...".format(process_name))
                if os.name == 'nt':
                    # On Windows, use taskkill to terminate the process tree
                    subprocess.run(['taskkill', '/F', '/T', '/PID', str(process.pid)], 
                                   capture_output=True, text=True)
                else:
                    process.terminate()
                    process.wait(timeout=10)
            except Exception as e:
                print("Error terminating {}: {}".format(process_name, e))
                try:
                    process.kill()
                except Exception:
                    pass

def main(duration_hours=1):
    log_file = "logs/training_runner.log"
    
    print("Starting training session for {} hours...".format(duration_hours))
    
    engine_process, rl_bot_process, ref_bot_process = start_engine_and_bots(log_file)
    
    if not all([engine_process, rl_bot_process, ref_bot_process]):
        print("Failed to start all required processes. Cleaning up...")
        processes = [
            ("Engine", engine_process),
            ("RL Bot", rl_bot_process),
            ("Reference Bot", ref_bot_process)
        ]
        terminate_processes(processes)
        return
    
    monitor = TrainingMonitor(log_file=log_file)
    
    start_time = time.time()
    end_time = start_time + (duration_hours * 60 * 60)
    
    processes = [
        ("Engine", engine_process),
        ("RL Bot", rl_bot_process),
        ("Reference Bot", ref_bot_process)
    ]
    
    try:
        while time.time() < end_time:
            # Check if any process has stopped
            for process_name, process in processes:
                if process and process.poll() is not None:
                    print("{} process has stopped.".format(process_name))
                    return

            monitor.parse_logs()
            monitor.plot_training_progress()
            
            # Print status update
            elapsed_hours = (time.time() - start_time) / 3600
            remaining_hours = duration_hours - elapsed_hours
            print("Training progress: {:.1f}h elapsed, {:.1f}h remaining".format(
                elapsed_hours, remaining_hours))
            
            time.sleep(60)  # Check for updates every minute

    except KeyboardInterrupt:
        print("Training interrupted by user.")
    finally:
        print("Terminating processes...")
        terminate_processes(processes)
        print("All processes terminated.")
        
        # Generate final report
        print("Generating final training report...")
        monitor.parse_logs()
        monitor.plot_training_progress()
        
        if monitor.rewards:
            print("Training Summary:")
            print("  Total training steps: {}".format(len(monitor.rewards)))
            print("  Average reward: {:.2f}".format(np.mean(monitor.rewards)))
            print("  Best reward: {:.2f}".format(np.max(monitor.rewards)))
            if monitor.inference_times:
                print("  Average inference time: {:.2f}ms".format(np.mean(monitor.inference_times)))
        else:
            print("No training data collected.")

if __name__ == "__main__":
    duration = 1
    if len(sys.argv) > 1:
        try:
            duration = float(sys.argv[1])
        except ValueError:
            print("Usage: python live_training.py [duration_in_hours]")
            sys.exit(1)
            
    main(duration_hours=duration) 