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

        with open(self.log_file, "r") as f:
            lines = f.readlines()

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
                    epsilon_str = line.split("Epsilon:")[1].split(",")[0].strip()
                    epsilon = float(epsilon_str)
                    self.epsilon_values.append(epsilon)

                    # Extract game number
                    game_str = line.split("Episode:")[1].split(",")[0].strip()
                    game = int(game_str)
                    self.games.append(game)

                    # Extract inference time
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
        plt.savefig(save_path)
        plt.close()
        print(f"Training progress plot saved to {save_path}")

def start_engine_and_bots(log_file):
    """Start the Zooscape engine and both bots in separate processes"""
    
    # --- Start Engine ---
    engine_path = os.path.join("..", "..", "engine", "Zooscape")
    engine_cmd = f"dotnet run --project {engine_path}"
    print(f"Starting Engine: {engine_cmd}")
    engine_log = open(os.path.join("logs", "engine.log"), "w")
    engine_process = subprocess.Popen(
        engine_cmd, shell=True, stdout=engine_log, stderr=engine_log, cwd=os.path.join("..", "..", "engine", "Zooscape")
    )
    print("Started Zooscape engine process.")

    # Wait for the engine to initialize
    print("Waiting for engine to start...")
    time.sleep(15)

    # --- Start RL Bot ---
    # Use the virtual environment's Python interpreter
    venv_python = os.path.join(".venv", "Scripts", "python.exe") if os.name == 'nt' else os.path.join(".venv", "bin", "python")
    rl_bot_cmd = f"{venv_python} training_bot_runner.py"
    print(f"Starting RL Bot: {rl_bot_cmd}")
    rl_log = open(log_file, "w")
    rl_bot_process = subprocess.Popen(
        rl_bot_cmd, shell=True, stdout=rl_log, stderr=rl_log
    )
    print("Started RL bot process.")

    # --- Start Reference Bot ---
    ref_bot_path = os.path.join("..", "..", "engine", "ReferenceBot")
    ref_bot_cmd = f"dotnet run --project {ref_bot_path}"
    print(f"Starting Reference Bot: {ref_bot_cmd}")
    ref_log = open(os.path.join("logs", "refbot.log"), "w")
    ref_bot_process = subprocess.Popen(
        ref_bot_cmd, shell=True, stdout=ref_log, stderr=ref_log, cwd=os.path.join("..", "..", "engine", "ReferenceBot")
    )
    print("Started Reference bot process.")

    return engine_process, rl_bot_process, ref_bot_process

def main(duration_hours=1):
    log_file = "logs/training_runner.log"
    engine_process, rl_bot_process, ref_bot_process = start_engine_and_bots(log_file)
    
    monitor = TrainingMonitor(log_file=log_file)
    
    start_time = time.time()
    end_time = start_time + (duration_hours * 60 * 60)
    
    try:
        while time.time() < end_time:
            if engine_process.poll() is not None:
                print("Engine process has stopped.")
                break
            if rl_bot_process.poll() is not None:
                print("RL Bot process has stopped.")
                break
            if ref_bot_process.poll() is not None:
                print("Reference Bot process has stopped.")
                break

            monitor.parse_logs()
            monitor.plot_training_progress()
            
            time.sleep(60) # Check for updates every minute

    except KeyboardInterrupt:
        print("Training interrupted by user.")
    finally:
        print("Terminating processes...")
        engine_process.terminate()
        rl_bot_process.terminate()
        ref_bot_process.terminate()
        
        # Wait for processes to terminate
        engine_process.wait()
        rl_bot_process.wait()
        ref_bot_process.wait()
        print("All processes terminated.")

if __name__ == "__main__":
    duration = 1
    if len(sys.argv) > 1:
        try:
            duration = float(sys.argv[1])
        except ValueError:
            print("Usage: python live_training.py [duration_in_hours]")
            sys.exit(1)
            
    main(duration_hours=duration) 