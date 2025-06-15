#!/usr/bin/env bash

# Relative path to the vcpkg toolchain file from the 'build' directory
VCPKG_TOOLCHAIN_FILE="../vcpkg/scripts/buildsystems/vcpkg.cmake"

echo "=== Building Advanced MCTS Bot ==="

# Create build directory if it doesn't exist
mkdir -p build

# Navigate into the build directory
pushd build

# Configure with CMake
echo "Configuring with CMake..."
cmake -DCMAKE_TOOLCHAIN_FILE=${VCPKG_TOOLCHAIN_FILE} \
      -DCMAKE_BUILD_TYPE=RelWithDebInfo \
      -DCMAKE_CXX_COMPILER=g++ \
      -S .. -B .

# Check if CMake configuration was successful
if [ $? -ne 0 ]; then
    echo "CMake configuration failed!"
    popd
    exit 1
fi

# Build the project
echo "Building project..."
make -j$(nproc)

# Check if build was successful
if [ $? -ne 0 ]; then
    echo "Build failed!"
    popd
    exit 1
fi

echo "Build successful!"
echo "Executable is in this directory (build/AdvancedMCTSBot)"

# Optional: Copy config file if needed and if it exists
if [ -f ../config.json ]; then
    echo "Copying config.json to build directory..."
    cp ../config.json .
fi

popd

echo "To run the bot (from project root c:\\dev\\2025-Zooscape\\Bots\\AdvancedMCTSBot):"
echo "  wsl bash -lc 'cd build && ./AdvancedMCTSBot'"