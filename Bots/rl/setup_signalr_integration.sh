#!/bin/bash

# Script to clone the Zooscape engine and set up the environment for testing with SignalR

echo "Setting up Zooscape engine and SignalR integration..."

# Create directories
mkdir -p zooscape_engine
mkdir -p logs

# Clone the Zooscape repository if it doesn't exist
if [ ! -d "zooscape_engine/2025-Zooscape" ]; then
    echo "Cloning Zooscape engine repository..."
    cd zooscape_engine
    git clone https://github.com/EntelectChallenge/2025-Zooscape.git
    cd ..
else
    echo "Zooscape engine repository already exists."
    # Pull latest changes
    cd zooscape_engine/2025-Zooscape
    git pull
    cd ../..
fi

# Install .NET SDK if not already installed
if ! command -v dotnet &> /dev/null; then
    echo "Installing .NET SDK..."
    wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
    sudo dpkg -i packages-microsoft-prod.deb
    rm packages-microsoft-prod.deb
    sudo apt-get update
    sudo apt-get install -y dotnet-sdk-6.0
else
    echo ".NET SDK already installed."
fi

# Install SignalR client packages
echo "Installing SignalR client packages..."
cd zooscape
dotnet add package Microsoft.AspNetCore.SignalR.Client
dotnet add package Microsoft.Extensions.Logging.Console
dotnet add package Microsoft.Extensions.Logging.Debug

# Build the SignalR bot client
echo "Building SignalR bot client..."
dotnet build

echo "Setup complete! You can now run the RL bot with the Zooscape engine."
echo "To start the engine: cd zooscape_engine/2025-Zooscape/Engine && dotnet run"
echo "To start the RL bot: cd zooscape && dotnet run --url http://localhost:5000/gameHub --botId RLBot"
