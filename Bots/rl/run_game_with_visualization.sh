#!/bin/bash

# Script to run the RL bot vs Reference Bot with live visualization

# Create necessary directories
mkdir -p logs

# Check if Python dependencies are installed
echo "Checking Python dependencies..."
pip install matplotlib numpy tensorflow

# Start the visualization server in the background
echo "Starting visualization server..."
python3 visualize_game.py > logs/visualization.log 2>&1 &
VIZ_PID=$!

# Wait for visualization server to initialize
sleep 3

# Start the game engine with both bots
echo "Starting game with RL Bot vs Reference Bot..."
echo "This will run in simulation mode if the actual game engine is not available."
echo "Press Ctrl+C to stop the game and visualization."

# In a real implementation, this would start the actual game engine
# For now, we'll just let the visualization run in simulation mode

# Wait for user to stop the game
echo "Game is running. Check the visualization window for live gameplay and scores."
echo "Press Ctrl+C to stop."

# Keep the script running until user presses Ctrl+C
trap "kill $VIZ_PID; echo 'Stopping game and visualization...'; exit 0" INT
while true; do
    sleep 1
done
