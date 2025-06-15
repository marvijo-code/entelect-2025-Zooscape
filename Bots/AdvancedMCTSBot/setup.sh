#!/usr/bin/env bash
set -ex

# --- Main Setup Script ---

main() {
  install_build_tools
  setup_vcpkg
  setup_compile_commands
}

# --- Function Definitions ---

install_build_tools() {
  echo "--- Installing build tools (build-essential) ---"
  # This requires sudo and may prompt for a password.
  sudo apt-get update
  sudo apt-get install -y build-essential
  echo "Build tools installed successfully."
}



setup_vcpkg() {
  echo "--- Setting up vcpkg ---"

  # 1. Clean Slate
  if [ -d ./vcpkg ]; then
    echo "Removing existing vcpkg directory for a fresh start..."
    rm -rf ./vcpkg
  fi

  # 2. Clone vcpkg
  echo "Cloning vcpkg repository..."
  git clone https://github.com/microsoft/vcpkg.git vcpkg

  # 3. CRITICAL: Verify the clone contains the required port
  echo "Verifying that the 'microsoft-signalr' port exists..."
  if [ ! -d "./vcpkg/ports/microsoft-signalr" ]; then
    echo "[FATAL ERROR] The 'microsoft-signalr' port was not found in the cloned vcpkg repository."
    echo "This indicates a problem with the 'git clone' operation."
    echo "Please check your network connection, git configuration, and try running the script again."
    exit 1
  fi
  echo "Verification successful. 'microsoft-signalr' port found."

  # 4. Bootstrap vcpkg
  echo "Bootstrapping vcpkg..."
  ./vcpkg/bootstrap-vcpkg.sh

  # 5. Install dependencies
  echo "Installing dependencies (fmt and microsoft-signalr)..."
  ./vcpkg/vcpkg install fmt
  ./vcpkg/vcpkg install microsoft-signalr
  echo "Dependency installation complete."
}

setup_compile_commands() {
  echo "--- Generating compile_commands.json ---"

  mkdir -p build/

  # Pass the vcpkg toolchain file to ensure CMake finds the packages
  cmake -DCMAKE_TOOLCHAIN_FILE=./vcpkg/scripts/buildsystems/vcpkg.cmake \
        -DCMAKE_EXPORT_COMPILE_COMMANDS=ON \
        -S . -B ./build

  if [ -f ./build/compile_commands.json ]; then
    echo "Successfully generated compile_commands.json."
    mv ./build/compile_commands.json ./
  else
    echo "[ERROR] Failed to generate compile_commands.json."
  fi

  # Clean up build directory
  if [ -d ./build ]; then
    find ./build -mindepth 1 -delete
  fi
}

# --- Script Execution ---
main
