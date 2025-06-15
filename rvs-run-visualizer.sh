#!/bin/bash

# Function to stop processes on a specific port
stop_process_on_port() {
    local port=$1
    local service_name=$2
    
    echo "Checking for processes using port $port ($service_name)..."
    local pid=$(lsof -ti :$port 2>/dev/null)
    if [ -n "$pid" ]; then
        echo "Stopping process with PID $pid using port $port..."
        kill -9 $pid
        echo "Process stopped."
    fi
}

# Stop existing processes
stop_process_on_port 5252 "Frontend"
stop_process_on_port 5008 "Visualizer API"

echo "Starting Zooscape Visualizer API and Frontend..."

# Get script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
API_DIR="$SCRIPT_DIR/visualizer-2d/api"
FRONTEND_DIR="$SCRIPT_DIR/visualizer-2d"

# Check if API directory exists
if [ ! -d "$API_DIR" ]; then
    echo "Error: API directory not found at $API_DIR"
    echo "Check that visualizer-2d/api exists in your project"
    exit 1
fi

# Start API
cd "$API_DIR"
npm start &

# Start frontend
cd "$FRONTEND_DIR"
npm run dev &
