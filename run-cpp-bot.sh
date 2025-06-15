#!/bin/bash

# Script to compile and run the AdvancedMCTSBot

# --- Configuration ---
# Adjust BOT_PROJECT_ROOT_WINDOWS if your C++ project is located elsewhere
# or if your WSL/Git Bash mounts C: drive differently (e.g., /c instead of /mnt/c)
BOT_PROJECT_ROOT_WINDOWS="c:\\dev\\2025-Zooscape"
BOT_SUBDIR="Bots/AdvancedMCTSBot" # Relative path from project root to this bot's CMakeLists.txt
EXECUTABLE_NAME="AdvancedMCTSBot" # As defined in add_executable() in CMakeLists.txt
BUILD_CONFIG="Release" # Can be Release, Debug, RelWithDebInfo, MinSizeRel etc.
# --- End Configuration ---

# Function to convert Windows path to a path usable in the current shell environment
convert_path() {
    local win_path="$1"
    # Basic conversion: replace backslashes and drive letters.
    # This heuristic might need adjustment based on your specific environment (WSL, Git Bash, Cygwin).
    if [[ "$OSTYPE" == "linux-gnu"* ]] && grep -qEi "(Microsoft|WSL)" /proc/version &> /dev/null; then
        # WSL: C:\ -> /mnt/c/
        echo "$win_path" | sed -e 's|\\\\|/|g' -e 's|C:|/mnt/c|I' -e 's|D:|/mnt/d|I' # Add other drives if needed
    elif [[ "$OSTYPE" == "cygwin"* ]] || [[ "$OSTYPE" == "msys"* ]]; then
        # Git Bash / Cygwin: C:\ -> /c/
        echo "$win_path" | sed -e 's|\\\\|/|g' -e 's|C:|/c|I' -e 's|D:|/d|I' # Add other drives if needed
    else
        # Assume it's a Unix-like path already or needs no conversion for this script's context
        echo "$win_path"
    fi
}

BOT_MODULE_ROOT_NATIVE=$(convert_path "${BOT_PROJECT_ROOT_WINDOWS}\\\\${BOT_SUBDIR}")
VCPKG_TOOLCHAIN_FILE_RELATIVE="vcpkg/scripts/buildsystems/vcpkg.cmake" # Relative to BOT_MODULE_ROOT_NATIVE

echo "INFO: Bot module root directory (native path): $BOT_MODULE_ROOT_NATIVE"

# Navigate to the bot's root directory
cd "$BOT_MODULE_ROOT_NATIVE" || { echo "ERROR: Failed to navigate to bot directory '$BOT_MODULE_ROOT_NATIVE'"; exit 1; }

# Define build directory name
BUILD_DIR_NAME="build"

# Create build directory if it doesn't exist
mkdir -p "$BUILD_DIR_NAME"

# Navigate to build directory
cd "$BUILD_DIR_NAME" || { echo "ERROR: Failed to navigate to build directory '$BUILD_DIR_NAME' inside '$BOT_MODULE_ROOT_NATIVE'"; exit 1; }

# Configure the project with CMake
echo "INFO: Configuring CMake from '$PWD'..."
# The CMAKE_TOOLCHAIN_FILE path is relative to the source directory (which is '..')
cmake .. \
    -DCMAKE_BUILD_TYPE="$BUILD_CONFIG" \
    -DCMAKE_TOOLCHAIN_FILE="../$VCPKG_TOOLCHAIN_FILE_RELATIVE" \
    || { echo "ERROR: CMake configuration failed"; exit 1; }

# Build the project
echo "INFO: Building project (Config: $BUILD_CONFIG)..."
cmake --build . --config "$BUILD_CONFIG" || { echo "ERROR: Build failed"; exit 1; }

# Determine executable path
# For single-configuration generators (like Makefiles on Linux), executable is often directly in build dir.
# For multi-configuration generators (like Visual Studio), it's often in a subdirectory named after the config.
# CMake also sometimes places executables in a 'bin' subdirectory of the build directory.

POSSIBLE_EXECUTABLE_PATH="./$EXECUTABLE_NAME" # Common for single-config (e.g. Unix Makefiles)

if [ ! -f "$POSSIBLE_EXECUTABLE_PATH" ] && [ -d "./$BUILD_CONFIG" ] && [ -f "./$BUILD_CONFIG/$EXECUTABLE_NAME" ]; then
    # Common for multi-config (e.g. Visual Studio) like ./Release/AdvancedMCTSBot
    POSSIBLE_EXECUTABLE_PATH="./$BUILD_CONFIG/$EXECUTABLE_NAME"
elif [ ! -f "$POSSIBLE_EXECUTABLE_PATH" ] && [ -d "./bin" ] && [ -f "./bin/$EXECUTABLE_NAME" ]; then
    # Sometimes output to build/bin/
    POSSIBLE_EXECUTABLE_PATH="./bin/$EXECUTABLE_NAME"
elif [ ! -f "$POSSIBLE_EXECUTABLE_PATH" ] && [ -d "./bin/$BUILD_CONFIG" ] && [ -f "./bin/$BUILD_CONFIG/$EXECUTABLE_NAME" ]; then
    # Sometimes output to build/bin/Release
    POSSIBLE_EXECUTABLE_PATH="./bin/$BUILD_CONFIG/$EXECUTABLE_NAME"
fi

# Run the executable
echo "INFO: Attempting to run '$EXECUTABLE_NAME' from '$POSSIBLE_EXECUTABLE_PATH'...'"
if [ -f "$POSSIBLE_EXECUTABLE_PATH" ]; then
    "$POSSIBLE_EXECUTABLE_PATH" "$@" # Pass any script arguments to the bot
else
    echo "ERROR: Executable '$EXECUTABLE_NAME' not found at expected path '$POSSIBLE_EXECUTABLE_PATH' or other common locations within '$PWD'."
    echo "Please check your CMake configuration ('add_executable', 'CMAKE_RUNTIME_OUTPUT_DIRECTORY') and build output."
    exit 1
fi

echo "INFO: Bot execution finished."
