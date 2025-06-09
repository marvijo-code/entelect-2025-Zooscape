#!/bin/bash

# Advanced MCTS Bot Build Script
echo "=== Building Advanced MCTS Bot ==="

# Create build directory
mkdir -p build
cd build

# Configure with CMake
echo "Configuring with CMake..."
cmake .. -DCMAKE_BUILD_TYPE=Release

# Build the project
echo "Building project..."
make -j$(nproc)

# Check if build was successful
if [ $? -eq 0 ]; then
    echo "Build successful!"
    echo "Executable: ./build/AdvancedMCTSBot"
    
    # Copy config file to build directory
    cp ../config.json .
    
    echo "To run the bot:"
    echo "  cd build"
    echo "  ./AdvancedMCTSBot"
else
    echo "Build failed!"
    exit 1
fi