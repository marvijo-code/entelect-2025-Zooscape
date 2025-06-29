#!/bin/bash
# Script to run the ZooscapeRunner GUI Application on WSL Ubuntu
# Professional Bot Manager with Integrated Visualizer Support

set -e  # Exit on any error

# Color definitions
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

# Default values
CONFIGURATION="Debug"
CLEAN_BUILD=false
SHOW_HELP=false

# Get absolute paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$SCRIPT_DIR"
ZOOSCAPE_RUNNER_DIR="$PROJECT_ROOT/ZooscapeRunner/ZooscapeRunner"
SOLUTION_FILE="$PROJECT_ROOT/ZooscapeRunner/ZooscapeRunner.sln"
PROJECT_FILE="$ZOOSCAPE_RUNNER_DIR/ZooscapeRunner.csproj"

# Function to display help
show_help() {
    echo -e "${CYAN}🦁 Zooscape Runner - Professional Bot Manager${NC}"
    echo ""
    echo "USAGE:"
    echo "    ./rui-run-frontend.sh [OPTIONS]"
    echo ""
    echo "OPTIONS:"
    echo "    -d, --debug     Run in Debug configuration (default)"
    echo "    -r, --release   Run in Release configuration"
    echo "    -c, --clean     Clean build before running"
    echo "    -h, --help      Show this help message"
    echo ""
    echo "FEATURES:"
    echo "    • 🤖 Complete bot management (8+ bot types)"
    echo "    • 📊 Integrated visualizer (API + Frontend)"
    echo "    • 🔧 Professional process management"
    echo "    • 🌐 One-click browser integration"
    echo "    • ⚡ Smart port conflict resolution"
    echo ""
    echo "PORTS:"
    echo "    • Engine: http://localhost:5000"
    echo "    • Visualizer API: http://localhost:5008"
    echo "    • Visualizer Frontend: http://localhost:5252"
    echo ""
    echo "EXAMPLES:"
    echo "    ./rui-run-frontend.sh                # Run in Debug mode"
    echo "    ./rui-run-frontend.sh --release      # Run in Release mode"
    echo "    ./rui-run-frontend.sh --clean        # Clean build first"
    echo ""
    echo "REQUIREMENTS:"
    echo "    • .NET 8.0 SDK"
    echo "    • WSL Ubuntu with GUI support (WSLg)"
    echo "    • X11 forwarding or Windows 11 with WSLg"
    echo ""
    echo "PATHS:"
    echo "    • Project Root: $PROJECT_ROOT"
    echo "    • ZooscapeRunner: $ZOOSCAPE_RUNNER_DIR"
    echo "    • Solution File: $SOLUTION_FILE"
    exit 0
}

# Function to log with colors
log_info() {
    echo -e "${GREEN}$1${NC}"
}

log_warn() {
    echo -e "${YELLOW}$1${NC}"
}

log_error() {
    echo -e "${RED}$1${NC}"
}

log_debug() {
    echo -e "${GRAY}$1${NC}"
}

log_cyan() {
    echo -e "${CYAN}$1${NC}"
}

# Function to check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Function to run dotnet command with error handling
run_dotnet_command() {
    local cmd="$1"
    local description="$2"
    local working_dir="${3:-$PROJECT_ROOT}"
    
    log_warn "🔄 $description..."
    log_debug "   Command: dotnet $cmd"
    log_debug "   Working Directory: $working_dir"
    
    if (cd "$working_dir" && dotnet $cmd); then
        log_info "✅ $description completed successfully"
        return 0
    else
        log_error "❌ $description failed"
        return 1
    fi
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -d|--debug)
            CONFIGURATION="Debug"
            shift
            ;;
        -r|--release)
            CONFIGURATION="Release"
            shift
            ;;
        -c|--clean)
            CLEAN_BUILD=true
            shift
            ;;
        -h|--help)
            show_help
            ;;
        *)
            log_error "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Show help if requested
if [ "$SHOW_HELP" = true ]; then
    show_help
fi

log_info "🚀 Starting Zooscape Runner - Professional Bot Manager"
log_cyan "📁 Project Root: $PROJECT_ROOT"
log_cyan "📁 ZooscapeRunner Directory: $ZOOSCAPE_RUNNER_DIR"
log_cyan "⚙️  Configuration: $CONFIGURATION"
log_cyan "🐧 Platform: WSL Ubuntu"

# Check prerequisites
log_cyan "🔍 Checking prerequisites..."

if ! command_exists dotnet; then
    log_error "❌ .NET SDK not found!"
    echo ""
    echo "Install .NET 8.0 SDK:"
    echo "  wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb"
    echo "  sudo dpkg -i packages-microsoft-prod.deb"
    echo "  sudo apt-get update"
    echo "  sudo apt-get install -y dotnet-sdk-8.0"
    exit 1
fi

# Check .NET version
DOTNET_VERSION=$(dotnet --version)
log_info "✅ Found .NET SDK: $DOTNET_VERSION"

# Check if we're in WSL
if [ -f /proc/version ] && grep -q Microsoft /proc/version; then
    log_info "✅ Running in WSL environment"
    
    # Check for GUI support
    if [ -n "$DISPLAY" ] || [ -n "$WAYLAND_DISPLAY" ]; then
        log_info "✅ GUI support detected"
    else
        log_warn "⚠️  No GUI environment detected"
        echo "   For GUI applications in WSL, you may need:"
        echo "   - Windows 11 with WSLg (recommended)"
        echo "   - X11 server on Windows (VcXsrv, Xming)"
        echo "   - Set DISPLAY environment variable"
    fi
else
    log_info "✅ Running on native Linux"
fi

# Check if project files exist
if [ ! -f "$SOLUTION_FILE" ]; then
    log_error "❌ Solution file not found at: $SOLUTION_FILE"
    log_warn "📁 Available solution files:"
    find "$PROJECT_ROOT" -name "*.sln" -type f | head -10
    exit 1
fi

if [ ! -f "$PROJECT_FILE" ]; then
    log_error "❌ Project file not found at: $PROJECT_FILE"
    log_warn "📁 Current directory contents:"
    ls -la "$ZOOSCAPE_RUNNER_DIR" 2>/dev/null || echo "Directory does not exist"
    exit 1
fi

log_info "✅ Found solution file: $SOLUTION_FILE"
log_info "✅ Found project file: $PROJECT_FILE"

# Check for processes.json
PROCESSES_JSON="$ZOOSCAPE_RUNNER_DIR/Assets/processes.json"
if [ ! -f "$PROCESSES_JSON" ]; then
    log_error "❌ processes.json not found at: $PROCESSES_JSON"
    exit 1
fi
log_info "✅ Found processes.json: $PROCESSES_JSON"

# Build and run the application
{
    # Clean build if requested
    if [ "$CLEAN_BUILD" = true ]; then
        log_warn "🧹 Cleaning solution..."
        if ! run_dotnet_command "clean \"$SOLUTION_FILE\" --configuration $CONFIGURATION" "Clean" "$PROJECT_ROOT"; then
            log_error "Clean operation failed"
            exit 1
        fi
    fi

    # Restore packages
    log_warn "📦 Restoring NuGet packages..."
    if ! run_dotnet_command "restore \"$SOLUTION_FILE\"" "Package restore" "$PROJECT_ROOT"; then
        log_error "Package restore failed"
        exit 1
    fi

    # Build the application
    log_warn "🔨 Building ZooscapeRunner..."
    if ! run_dotnet_command "build \"$SOLUTION_FILE\" --configuration $CONFIGURATION --no-restore" "Build" "$PROJECT_ROOT"; then
        log_error "Build failed"
        exit 1
    fi

    log_info ""
    log_info "🎉 Build completed successfully!"
    log_info "🚀 Starting ZooscapeRunner GUI Application..."
    log_info ""
    log_info "FEATURES AVAILABLE:"
    log_info "• 🤖 Bot Management: Start/Stop/Restart all bots"
    log_info "• 📊 Visualizer: Integrated API + Frontend management"
    log_info "• 🌐 Browser Integration: One-click visualizer access"
    log_info "• 📋 Real-time Logs: Monitor all processes"
    log_info "• ⚡ Smart Port Management: Automatic conflict resolution"
    log_info ""

    # Run the application
    log_cyan "▶️  Launching application..."
    
    # Check for executable first
    APP_PATH="$ZOOSCAPE_RUNNER_DIR/bin/$CONFIGURATION/net8.0-windows10.0.19041.0/ZooscapeRunner.exe"
    
    if [ -f "$APP_PATH" ]; then
        log_info "✅ Found application executable: $APP_PATH"
        log_info "🚀 Starting GUI application..."
        
        # Try to run the Windows executable in WSL
        if command_exists explorer.exe; then
            # We're in WSL, try to run the Windows executable
            log_cyan "🪟 Attempting to run Windows executable via WSL..."
            # Set working directory to ZooscapeRunner directory so it finds Assets/processes.json
            (cd "$ZOOSCAPE_RUNNER_DIR" && "$APP_PATH") &
        else
            log_warn "⚠️  Cannot run Windows executable directly"
            log_cyan "🔄 Falling back to dotnet run..."
            (cd "$ZOOSCAPE_RUNNER_DIR" && dotnet run --project ZooscapeRunner.csproj --configuration "$CONFIGURATION") &
        fi
    else
        # Fallback: run with dotnet run
        log_warn "⚠️  Executable not found, using 'dotnet run' instead..."
        log_cyan "🚀 Starting with dotnet run..."
        
        # Use dotnet run for development - IMPORTANT: run from ZooscapeRunner directory
        log_debug "▶️  Running: dotnet run --project ZooscapeRunner.csproj --configuration $CONFIGURATION"
        log_debug "▶️  Working Directory: $ZOOSCAPE_RUNNER_DIR"
        (cd "$ZOOSCAPE_RUNNER_DIR" && dotnet run --project ZooscapeRunner.csproj --configuration "$CONFIGURATION") &
    fi

    # Get the PID of the background process
    APP_PID=$!
    
    log_info ""
    log_info "✅ ZooscapeRunner GUI Application launched successfully!"
    log_info ""
    log_info "🎯 QUICK START GUIDE:"
    log_info "1. Click \"▶️ Start All Bots\" to launch the bot ecosystem"
    log_info "2. Click \"▶️ Start Visualizer\" to launch the visualization stack"
    log_info "3. Click \"🌐 Open in Browser\" to view the visualizer frontend"
    log_info "4. Monitor real-time logs in the application interface"
    log_info ""
    log_info "📊 PORTS:"
    log_info "• Engine: http://localhost:5000"
    log_info "• Visualizer API: http://localhost:5008"
    log_info "• Visualizer Frontend: http://localhost:5252"
    log_info ""
    log_cyan "🔧 Application started with PID: $APP_PID"
    log_cyan "   Application working directory: $ZOOSCAPE_RUNNER_DIR"
    log_cyan "   Press Ctrl+C to stop this script (app will continue running)"
    log_info ""

    # Wait for the application or user interrupt
    trap 'log_warn "🛑 Script interrupted. Application may still be running."; exit 0' INT
    
    # Check if the process is still running
    if kill -0 $APP_PID 2>/dev/null; then
        log_info "✅ Application is running. Use 'kill $APP_PID' to stop it if needed."
        wait $APP_PID
    else
        log_warn "⚠️  Application may have failed to start or exited immediately."
        log_info "Check the output above for any error messages."
    fi

} || {
    log_error ""
    log_error "❌ ERROR: Failed to start ZooscapeRunner"
    log_error ""
    log_error "🔧 TROUBLESHOOTING:"
    log_error "1. Ensure .NET 8.0 SDK is installed"
    log_error "2. Check that all NuGet packages are restored"
    log_error "3. Verify project builds successfully"
    log_error "4. Try running with --clean parameter"
    log_error "5. For WSL GUI issues:"
    log_error "   - Ensure Windows 11 with WSLg, or"
    log_error "   - Install X11 server (VcXsrv) on Windows"
    log_error "   - Set DISPLAY variable: export DISPLAY=:0"
    log_error ""
    log_error "💡 For help: ./rui-run-frontend.sh --help"
    log_error ""
    
    exit 1
}

log_cyan "🏁 Script execution completed." 