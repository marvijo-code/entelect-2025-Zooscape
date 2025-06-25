# ZooscapeRunner UI Enhancement Plan

## Current Issues Identified

### 1. **Application Stability Issues**
- App terminates abruptly after clicking "Start All"
- Shows "Building..." status but then crashes
- No visible error messages or logs
- Silent failures without user feedback

### 2. **Missing Features**
- Auto-restart timer is not configurable
- No toggle to enable/disable auto-restart
- No real-time logging/output display
- Limited process control options

### 3. **UI/UX Concerns**
- Basic appearance - not professional looking
- No visual feedback for long-running operations
- Limited status information
- No progress indicators for builds

## Comprehensive Enhancement Plan

### Phase 1: Stability & Logging (Priority: Critical)

#### 1.1 Enhanced Error Handling & Logging
```csharp
// Add comprehensive logging system
- Implement Serilog for structured logging
- Create log viewer panel in UI
- Add real-time log streaming
- Capture build output and errors
- Add crash reporting with stack traces
```

**Implementation Tasks:**
- [ ] Add Serilog NuGet package
- [ ] Create `LoggingService` with file and UI output
- [ ] Add `LogViewerControl` with scrollable text area
- [ ] Implement real-time log streaming to UI
- [ ] Add crash detection and recovery

#### 1.2 Process Management Improvements
```csharp
// Robust process handling
- Add process lifecycle monitoring
- Implement graceful shutdown handling
- Add build timeout detection
- Capture and display process output
- Add retry mechanisms for failed operations
```

**Implementation Tasks:**
- [ ] Enhance `ProcessManager` with better error handling
- [ ] Add process output capture and display
- [ ] Implement build progress tracking
- [ ] Add timeout handling for long-running builds
- [ ] Create process health monitoring

### Phase 2: Professional UI Design (Priority: High)

#### 2.1 Modern Visual Design
```xaml
<!-- Professional styling with Material Design -->
<Style TargetType="Button" x:Key="PrimaryButton">
    <Setter Property="Background" Value="#2196F3"/>
    <Setter Property="Foreground" Value="White"/>
    <Setter Property="CornerRadius" Value="4"/>
    <Setter Property="Padding" Value="16,8"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
</Style>
```

**Design Elements:**
- [ ] Material Design color scheme (Primary: #2196F3, Accent: #FF4081)
- [ ] Modern typography (Segoe UI Variable)
- [ ] Consistent spacing and padding (8px grid system)
- [ ] Professional icons (Fluent UI Icons)
- [ ] Subtle shadows and elevation
- [ ] Smooth animations and transitions

#### 2.2 Enhanced Layout Structure
```
┌─────────────────────────────────────────────────────────────┐
│ ZooscapeRunner - Professional Process Manager              │
├─────────────────────────────────────────────────────────────┤
│ [≡] Menu    [🔄] Refresh    [⚙️] Settings    [❓] Help      │
├─────────────────────────────────────────────────────────────┤
│ ┌─── Control Panel ────┐  ┌─── Auto-Restart ─────────────┐ │
│ │ [▶️ Start All]       │  │ ☑️ Enable Auto-Restart      │ │
│ │ [⏹️ Stop All]        │  │ ⏱️ Interval: [180] seconds   │ │
│ │ [🔄 Restart All]     │  │ ⏸️ [Pause] [▶️ Resume]       │ │
│ └─────────────────────┘  └──────────────────────────────┘ │
├─────────────────────────────────────────────────────────────┤
│ ┌─── Process Status ──────────────────────────────────────┐ │
│ │ Process Name        │ Status      │ Actions    │ Logs  │ │
│ │ ─────────────────── │ ─────────── │ ────────── │ ───── │ │
│ │ 🎮 Zooscape Engine  │ ✅ Running  │ [⏹️][📊]   │ [📋]  │ │
│ │ 🤖 ClingyHeuroBot2  │ 🔄 Building │ [⏹️][📊]   │ [📋]  │ │
│ │ 🧠 AdvancedMCTSBot  │ ❌ Failed   │ [▶️][📊]   │ [📋]  │ │
│ └─────────────────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────────┤
│ ┌─── Live Logs & Output ──────────────────────────────────┐ │
│ │ [2025-01-20 10:30:15] Building ClingyHeuroBot2...      │ │
│ │ [2025-01-20 10:30:16] dotnet build started             │ │
│ │ [2025-01-20 10:30:18] Build completed successfully     │ │
│ │ [2025-01-20 10:30:19] Starting process...              │ │
│ │ [📤 Export] [🗑️ Clear] [🔍 Filter: [All Levels ▼]]     │ │
│ └─────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### Phase 3: Advanced Features (Priority: Medium)

#### 3.1 Configurable Auto-Restart System
```csharp
public class AutoRestartSettings
{
    public bool IsEnabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 180;
    public bool PauseOnFailure { get; set; } = true;
    public List<string> ExcludedProcesses { get; set; } = new();
}
```

**Features:**
- [ ] Toggle auto-restart on/off
- [ ] Configurable restart interval (30s - 30min)
- [ ] Pause/Resume functionality
- [ ] Process-specific exclusions
- [ ] Restart on failure detection
- [ ] Visual countdown with progress ring

#### 3.2 Advanced Process Controls
```csharp
// Per-process actions
- Individual start/stop/restart buttons
- Process health monitoring
- Resource usage display (CPU, Memory)
- Process logs viewer
- Custom environment variables editor
- Process priority settings
```

#### 3.3 Settings & Configuration
```xaml
<!-- Settings Panel -->
<StackPanel>
    <TextBlock Text="General Settings" Style="{StaticResource SectionHeader}"/>
    <ToggleSwitch Header="Start minimized to system tray"/>
    <ToggleSwitch Header="Auto-start processes on launch"/>
    <NumberBox Header="Log retention days" Value="7"/>
    
    <TextBlock Text="Auto-Restart Settings" Style="{StaticResource SectionHeader}"/>
    <ToggleSwitch x:Name="AutoRestartToggle" Header="Enable auto-restart"/>
    <Slider Header="Restart interval (seconds)" Minimum="30" Maximum="1800" Value="180"/>
    <ToggleSwitch Header="Pause on build failures"/>
</StackPanel>
```

### Phase 4: Performance & Polish (Priority: Low)

#### 4.1 Performance Optimizations
- [ ] Async/await for all I/O operations
- [ ] Background thread for process monitoring
- [ ] Efficient log buffering and display
- [ ] Memory management for long-running sessions
- [ ] Startup time optimization

#### 4.2 User Experience Enhancements
- [ ] Keyboard shortcuts (Ctrl+S for Start All, etc.)
- [ ] Context menus for processes
- [ ] Drag & drop for process reordering
- [ ] Export logs to file
- [ ] Import/Export configuration
- [ ] Dark/Light theme toggle

## Implementation Timeline

### Week 1: Foundation & Stability
- [ ] Day 1-2: Implement logging system and error handling
- [ ] Day 3-4: Fix process management and build issues
- [ ] Day 5: Add basic log viewer to UI

### Week 2: Professional UI
- [ ] Day 1-2: Design system and styling
- [ ] Day 3-4: Enhanced layout and controls
- [ ] Day 5: Icons, animations, and polish

### Week 3: Advanced Features
- [ ] Day 1-2: Configurable auto-restart system
- [ ] Day 3-4: Settings panel and configuration
- [ ] Day 5: Advanced process controls

### Week 4: Testing & Polish
- [ ] Day 1-2: Comprehensive testing
- [ ] Day 3-4: Performance optimization
- [ ] Day 5: Final polish and documentation

## Technical Architecture

### Logging System
```csharp
public interface ILoggingService
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception ex = null);
    IObservable<LogEntry> LogStream { get; }
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Source { get; set; }
    public string Message { get; set; }
    public string Details { get; set; }
}
```

### Settings Management
```csharp
public interface ISettingsService
{
    T GetSetting<T>(string key, T defaultValue = default);
    void SetSetting<T>(string key, T value);
    void SaveSettings();
    IObservable<SettingChanged> SettingChanges { get; }
}
```

### Enhanced Process Manager
```csharp
public interface IEnhancedProcessManager : IProcessManager
{
    IObservable<ProcessStatusChanged> StatusChanges { get; }
    IObservable<ProcessOutput> OutputReceived { get; }
    Task<bool> StartProcessAsync(string processName, CancellationToken cancellationToken);
    Task<bool> StopProcessAsync(string processName, CancellationToken cancellationToken);
    ProcessMetrics GetProcessMetrics(string processName);
}
```

## File Structure Changes

```
ZooscapeRunner/
├── Services/
│   ├── ILoggingService.cs
│   ├── LoggingService.cs
│   ├── ISettingsService.cs
│   ├── SettingsService.cs
│   └── EnhancedProcessManager.cs
├── Controls/
│   ├── LogViewerControl.xaml
│   ├── ProcessControlPanel.xaml
│   ├── SettingsPanel.xaml
│   └── StatusIndicator.xaml
├── Themes/
│   ├── Generic.xaml
│   ├── LightTheme.xaml
│   └── DarkTheme.xaml
├── Models/
│   ├── LogEntry.cs
│   ├── AppSettings.cs
│   └── ProcessMetrics.cs
└── Assets/
    ├── Icons/
    └── Styles/
```

## Success Metrics

### Stability
- [ ] Zero crashes during normal operation
- [ ] 100% error scenarios handled gracefully
- [ ] All build failures properly reported
- [ ] Process recovery rate > 95%

### User Experience
- [ ] Professional appearance rating > 8/10
- [ ] Task completion time reduced by 50%
- [ ] User satisfaction rating > 9/10
- [ ] Zero confusion about app state

### Performance
- [ ] Startup time < 3 seconds
- [ ] UI responsiveness during builds
- [ ] Memory usage < 100MB
- [ ] Log processing < 1ms per entry

This plan addresses all identified issues and provides a roadmap for creating a professional, stable, and feature-rich process management application. 