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

    def __init__(self, log_file="logs/rl_bot.log"):
        self.log_file = log_file
        self.rewards = []
        self.pellets = []
        self.captures = []
        self.inference_times = []
        self.epsilon_values = []
        self.games = []

    def parse_logs(self):
        """Parse the log file to extract training metrics"""
        if not os.path.exists(self.log_file):
            print(f"Log file {self.log_file} not found.")
            return

        with open(self.log_file, "r") as f:
            lines = f.readlines()

        for line in lines:
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
            print("No training data found.")
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
        plt.plot(self.inference_times)
        plt.axhline(y=150, color="r", linestyle="--", label="150ms Limit")
        plt.title("Average Inference Time")
        plt.xlabel("Training Step")
        plt.ylabel("Time (ms)")
        plt.legend()

        plt.tight_layout()
        plt.savefig(save_path)
        print(f"Training progress plot saved to {save_path}")

    def generate_performance_report(self, save_path="results/performance_report.json"):
        """Generate a performance report with key metrics"""
        if not self.rewards:
            self.parse_logs()

        if not self.rewards:
            print("No training data found.")
            return

        report = {
            "total_training_steps": len(self.rewards),
            "total_games": len(self.pellets) if self.pellets else 0,
            "average_reward": (
                sum(self.rewards) / len(self.rewards) if self.rewards else 0
            ),
            "max_reward": max(self.rewards) if self.rewards else 0,
            "min_reward": min(self.rewards) if self.rewards else 0,
            "average_pellets_per_game": (
                sum(self.pellets) / len(self.pellets) if self.pellets else 0
            ),
            "total_pellets": sum(self.pellets) if self.pellets else 0,
            "total_captures": sum(self.captures) if self.captures else 0,
            "average_inference_time": (
                sum(self.inference_times) / len(self.inference_times)
                if self.inference_times
                else 0
            ),
            "max_inference_time": (
                max(self.inference_times) if self.inference_times else 0
            ),
            "min_inference_time": (
                min(self.inference_times) if self.inference_times else 0
            ),
            "percentage_under_150ms": (
                sum(1 for t in self.inference_times if t < 150)
                / len(self.inference_times)
                * 100
                if self.inference_times
                else 0
            ),
        }

        with open(save_path, "w") as f:
            json.dump(report, f, indent=4)

        print(f"Performance report saved to {save_path}")
        return report


def start_engine_and_bots():
    """Start the Zooscape engine and both bots in separate processes"""
    # Check if the engine repository exists
    if not os.path.exists("zooscape_engine/2025-Zooscape"):
        print(
            "Zooscape engine not found. Please run setup_signalr_integration.sh first."
        )
        return None, None, None

    # Start the engine
    engine_cmd = "cd zooscape_engine/2025-Zooscape/Engine && dotnet run"
    engine_process = subprocess.Popen(
        engine_cmd, shell=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE
    )
    print("Started Zooscape engine.")

    # Wait for the engine to initialize
    time.sleep(5)

    # Start the RL bot
    rl_bot_cmd = (
        "cd zooscape && dotnet run --url http://localhost:5000/bothub --botId RLBot"
    )
    rl_bot_process = subprocess.Popen(
        rl_bot_cmd, shell=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE
    )
    print("Started RL bot.")

    # Start the Reference bot
    ref_bot_cmd = "cd zooscape_engine/2025-Zooscape/ReferenceBot && dotnet run --url http://localhost:5000/bothub --botId RefBot"
    ref_bot_process = subprocess.Popen(
        ref_bot_cmd, shell=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE
    )
    print("Started Reference bot.")

    return engine_process, rl_bot_process, ref_bot_process


def monitor_processes(processes, duration_hours=24):
    """Monitor the running processes and restart if needed"""
    start_time = time.time()
    end_time = start_time + (duration_hours * 60 * 60)

    try:
        while time.time() < end_time:
            all_running = True
            for i, (name, process) in enumerate(processes):
                if process.poll() is not None:
                    print(f"{name} has stopped. Restarting...")
                    # Restart the process
                    if name == "Engine":
                        cmd = "cd zooscape_engine/2025-Zooscape/Engine && dotnet run"
                    elif name == "RL Bot":
                        cmd = "cd zooscape && dotnet run --url http://localhost:5000/bothub --botId RLBot"
                    elif name == "Ref Bot":
                        cmd = "cd zooscape_engine/2025-Zooscape/ReferenceBot && dotnet run --url http://localhost:5000/bothub --botId RefBot"

                    processes[i] = (
                        name,
                        subprocess.Popen(
                            cmd,
                            shell=True,
                            stdout=subprocess.PIPE,
                            stderr=subprocess.PIPE,
                        ),
                    )
                    all_running = False

            # Generate training progress plots periodically
            if int(time.time() - start_time) % 3600 == 0:  # Every hour
                monitor = TrainingMonitor()
                monitor.plot_training_progress()
                monitor.generate_performance_report()

            time.sleep(60)  # Check every minute

    except KeyboardInterrupt:
        print("Training interrupted by user.")
    finally:
        # Stop all processes
        for name, process in processes:
            if process.poll() is None:
                print(f"Stopping {name}...")
                process.terminate()
                try:
                    process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    process.kill()


def main():
    print("Starting training and evaluation of RL bot against Reference Bot...")

    # Start the engine and bots
    engine_process, rl_bot_process, ref_bot_process = start_engine_and_bots()

    if not all([engine_process, rl_bot_process, ref_bot_process]):
        print("Failed to start all required processes.")
        return

    # Monitor the processes
    processes = [
        ("Engine", engine_process),
        ("RL Bot", rl_bot_process),
        ("Ref Bot", ref_bot_process),
    ]

    print("All processes started. Training will run for 24 hours.")
    print("Press Ctrl+C to stop training early.")

    # Start monitoring in a separate thread
    monitor_thread = threading.Thread(target=monitor_processes, args=(processes,))
    monitor_thread.start()

    try:
        # Wait for the monitoring thread to finish
        monitor_thread.join()
    except KeyboardInterrupt:
        print("Training interrupted by user.")

    # Generate final reports
    print("Generating final training reports...")
    monitor = TrainingMonitor()
    monitor.plot_training_progress()
    report = monitor.generate_performance_report()

    # Print summary
    print("\nTraining Summary:")
    print(f"Total training steps: {report['total_training_steps']}")
    print(f"Total games: {report['total_games']}")
    print(f"Average reward: {report['average_reward']:.2f}")
    print(f"Total pellets collected: {report['total_pellets']}")
    print(f"Average inference time: {report['average_inference_time']:.2f}ms")
    print(f"Percentage under 150ms: {report['percentage_under_150ms']:.2f}%")

    print(
        "\nTraining complete! Check the results directory for detailed reports and visualizations."
    )


if __name__ == "__main__":
    main()
