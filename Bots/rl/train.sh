#!/bin/bash

# Script to run the training and evaluation of the RL bot

# Create necessary directories
mkdir -p models results logs

# Set up Python environment
echo "Setting up Python environment..."
pip install matplotlib

# Run the training script
echo "Starting RL bot training and evaluation..."
python3 train_rl_bot.py > logs/training.log 2>&1

echo "Training and evaluation completed. Check logs/training.log for details."
