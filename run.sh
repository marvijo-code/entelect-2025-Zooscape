#!/bin/bash

# Set environment variables
export HUSKY=0
export ASPNETCORE_URLS="http://localhost:5000"

# Create logs directory if it doesn't exist
LOGS_PATH="$(dirname "$0")/logs"
if [ ! -d "$LOGS_PATH" ]; then
    mkdir -p "$LOGS_PATH"
    echo -e "\033[33m📁 Created logs directory at: $LOGS_PATH\033[0m"
fi
export LOG_DIR="$LOGS_PATH"

echo -e "\033[36m🎮 Building Zooscape in Release configuration...\033[0m"

# Restore dependencies
dotnet restore ./Zooscape/Zooscape.csproj

# Build the project
dotnet build ./Zooscape/Zooscape.csproj -c Release

echo -e "\033[32m🚀 Starting Zooscape server...\033[0m"

# Run the application
dotnet run --project ./Zooscape/Zooscape.csproj -c Release --no-build

# Note: The server will be available at:
# - Main endpoint: http://localhost:5000
# - Health check: http://localhost:5000/bothub 