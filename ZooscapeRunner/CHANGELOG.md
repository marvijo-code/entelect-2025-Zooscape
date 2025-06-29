# Changelog

All notable changes to the Zooscape Runner project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Toggle Buttons**: Replaced separate start/stop buttons with single toggle buttons that change based on process state
- **Colored Logs**: Added rich text formatting with color-coded log messages:
  - 🔴 Red: Errors, exceptions, failures
  - 🟠 Orange: Warnings
  - 🟢 Green: Success, completed, started messages
  - 🔵 Light Blue: Info, build, restore messages
  - 🟦 Cyan: Console output (Debug.WriteLine, Console.WriteLine)
  - ⚪ White: Default text
- **Process Type Classification**: Added ProcessType property to distinguish between Bot and Visualizer processes
- **Dynamic Button States**: Buttons automatically update their appearance and command based on process status
- **Professional Bash Script**: Created `rui-run-frontend.sh` with comprehensive features:
  - Color-coded logging system (info, warn, error, debug, cyan)
  - Cross-platform compatibility checks (WSL detection, GUI support)
  - Absolute path resolution for all file operations
  - Prerequisite validation (.NET SDK, project files, processes.json)
  - Background process management with PID tracking
  - Professional help system with feature overview

### Fixed
- **Layout Issues**: Fixed process list being cut off by logs panel
  - Changed process list row to use `Height="2*"` for more space
  - Set logs panel to `MaxHeight="250"` to prevent overflow
  - Improved collapsible logs panel behavior
- **Toggle Button Logic**: Implemented proper state management for bot and visualizer toggle buttons
- **Visualizer API**: Confirmed API is working correctly on http://localhost:5008
- **Rich Text Logs**: Replaced simple TextBlock with RichTextBlock for colored log display
- **Process Status Detection**: Enhanced status checking to properly identify running processes

### Changed
- **UI Layout**: Modified grid layout to give more space to process list
- **Button Behavior**: Single toggle buttons instead of separate start/stop buttons
- **Log Display**: Enhanced with color coding and better formatting
- **Process Management**: Added ProcessType property for better categorization

### Technical Details
- **Files Modified**: MainPage.xaml, MainPage.xaml.cs, ProcessViewModel.cs, ProcessManager.cs, ManagedProcess.cs
- **UI Enhancements**: 
  - Added `ToggleBotsButton` and `ToggleVisualizerButton` with dynamic state management
  - Implemented `UpdateButtonStates()` method for real-time button updates
  - Added `ParseAndDisplayColoredLogs()` for rich text formatting
  - Added `GetLogLineColor()` for intelligent color selection
- **State Management**: Added `_areBotsRunning` and `_isVisualizerRunning` boolean fields
- **Process Classification**: Added ProcessType property with "Bot" and "Visualizer" values
- **Cross-Platform**: Bash script includes WSL detection and Windows executable launching

## [Previous] - 2024-12-XX

### Added
- Visualizer integration with API and Frontend services
- Enhanced process management with port conflict resolution
- Professional UI with separated bot and visualizer controls
- Cross-platform browser integration for visualizer access
- Real-time status updates with port information
- Enhanced ProcessManager with visualizer-specific methods
- Process type classification (Bot vs Visualizer)
- Comprehensive error handling and logging
- Professional styling with emoji indicators
- Smart port management with automatic cleanup

## [2.1.0] - 2025-01-19

### 🚀 Major New Features
- **🎯 Integrated Visualizer Management**: Full integration of `rv-run-visualizer.ps1` functionality
- **📊 Complete Visualization Stack**: API + Frontend management with one-click deployment
- **🔗 Browser Integration**: Automatic browser launch for visualizer frontend
- **⚡ Smart Port Management**: Automatic port conflict detection and cleanup

### ✨ Visualizer Features Added
- **Dual-Service Architecture**:
  - Visualizer API (dotnet watch FunctionalTests) on port 5008
  - Frontend (npm run dev visualizer-2d) on port 5252
  - Independent start/stop controls for each service
  - Automatic port cleanup before startup

- **Enhanced UI Controls**:
  - Separate bot management and visualizer sections
  - Start/Stop Visualizer buttons with status indicators
  - "Open in Browser" button for instant frontend access
  - Real-time status updates for visualizer services

- **Advanced Port Management**:
  - Windows netstat integration for port discovery
  - Automatic process termination on conflicting ports
  - Port availability validation before service startup
  - Graceful cleanup on application shutdown

- **Process Type Classification**:
  - Bot processes vs Visualizer processes separation
  - Service-specific management and monitoring
  - Independent lifecycle management
  - Targeted start/stop operations

### 🔧 Technical Enhancements
- **ProcessConfig Extensions**:
  - Added `RequiredPorts` array for port dependency tracking
  - Added `ProcessType` field for service classification
  - Enhanced JSON serialization context for new properties

- **ProcessManager Capabilities**:
  - `StartVisualizerAsync()` / `StopVisualizerAsync()` methods
  - `StopProcessOnPortAsync()` for targeted port cleanup
  - Service-specific process management
  - Enhanced error handling and logging

- **Cross-Platform Browser Support**:
  - Windows: Direct ProcessStartInfo launch
  - Android: Intent-based browser launching
  - iOS/macOS: System launcher integration
  - Fallback support for other platforms

### 📱 User Experience
- **Intuitive Interface**:
  - Logical separation of bot and visualizer controls
  - Clear visual hierarchy with emoji indicators
  - Status messages with port information
  - Error handling with helpful user guidance

- **One-Click Workflow**:
  - Single button to start entire visualization stack
  - Automatic dependency management
  - Browser opens automatically to frontend
  - Clean shutdown of all services

### 🔄 PowerShell Script Parity
- **rv-run-visualizer.ps1 Features Migrated**:
  - Port 5252/5008 management ✅
  - Service startup/shutdown ✅
  - Real-time monitoring ✅
  - Error handling and cleanup ✅
  - Process lifecycle management ✅

### 📊 Monitoring & Logging
- **Enhanced Logging**:
  - Visualizer-specific log categorization
  - Port management operation logging
  - Service startup/shutdown events
  - Error reporting with actionable information

---

## [2.0.0-professional] - 2025-01-19

### 🚀 Major Enhancements
- **Complete PowerShell Script Migration**: All functionality from `ra-run-all-local.ps1` now available in GUI
- **Professional Project Configuration**: Enhanced `.csproj` with comprehensive dependencies and metadata
- **Multi-Bot Support**: Added support for all bot types (C#, C++, experimental variants)
- **Advanced Process Management**: Intelligent build orchestration and process lifecycle management

### ✨ Added Features
- **Comprehensive Dependency Management**: 
  - Process management libraries (System.Diagnostics.Process, System.Management)
  - Advanced logging framework (Serilog with multiple sinks)
  - Configuration management (Microsoft.Extensions.Configuration.*)
  - Networking libraries for port management
  - JSON serialization for configuration handling
  - Windows-specific integrations for terminal management

- **Enhanced Bot Configuration**:
  - Support for 8 different bot types
  - Automatic GUID generation for bot tokens
  - Release configuration builds by default
  - Proper environment variable handling
  - C++ bot build integration

- **Professional UI/UX**:
  - Material Design interface
  - Real-time log streaming
  - Process status indicators
  - Auto-restart countdown display
  - Error handling and recovery

- **Advanced Process Features**:
  - Port availability checking (5000)
  - Graceful process termination
  - Build failure detection and reporting
  - Real-time log aggregation
  - Process cleanup on exit

### 🔧 Technical Improvements
- **Build System**: 
  - Added Release/Debug configurations
  - Performance optimizations (trimming, compression)
  - Code analysis integration
  - Assembly metadata for professional branding

- **Package Management**:
  - Version conflict resolution
  - Security vulnerability fixes
  - Dependency optimization
  - Platform-specific package inclusion

- **Architecture**:
  - MVVM pattern implementation
  - Dependency injection container
  - Service-oriented architecture
  - Cross-platform compatibility preparation

### 📄 Documentation
- **Comprehensive README**: Feature overview, setup instructions, usage guide
- **Migration Guide**: PowerShell script to GUI application mapping
- **Technical Documentation**: Architecture details, configuration options
- **Development Guide**: Contributing guidelines, debugging instructions

### 🔄 Migration from v1.0.0
- **Feature Parity**: All PowerShell script functionality preserved
- **Enhanced UX**: GUI replaces command-line interactions
- **Improved Reliability**: Better error handling and process management
- **Professional Appearance**: Branded application with proper metadata

### 🛠️ Configuration Changes
- **processes.json**: Updated with all bot configurations
- **Environment Variables**: Standardized TOKEN/BOT_TOKEN handling
- **Build Commands**: Consistent Release configuration usage
- **Working Directories**: Proper path resolution for all bots

### 📊 Performance
- **Startup Time**: Optimized dependency loading
- **Memory Usage**: Efficient log management and process monitoring
- **Resource Cleanup**: Proper disposal of system resources
- **Build Performance**: Parallel build capabilities where possible

### 🔒 Security
- **Token Management**: Secure GUID generation for bot authentication
- **Process Isolation**: Proper process boundary management
- **Port Security**: Port conflict detection and resolution
- **Error Disclosure**: Safe error reporting without sensitive data exposure

---

## [1.0.0] - Previous Version
### Initial Features
- Basic Uno Platform setup
- Simple process management
- Basic UI components
- Limited bot support

---

### Legend
- 🚀 Major Features
- ✨ New Features  
- 🔧 Improvements
- 🐛 Bug Fixes
- 🔒 Security
- 📄 Documentation
- ⚠️ Breaking Changes 