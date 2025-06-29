# Script to run the ZooscapeRunner GUI Application
# Professional Bot Manager with Integrated Visualizer Support

param(
    [switch]$Debug,
    [switch]$Release,
    [switch]$Clean,
    [switch]$ShowHelp
)

if ($ShowHelp) {
    Write-Host @"
🦁 Zooscape Runner - Professional Bot Manager

USAGE:
    .\rui-run-frontend.ps1 [OPTIONS]

OPTIONS:
    -Debug      Run in Debug configuration (default)
    -Release    Run in Release configuration  
    -Clean      Clean build before running
    -ShowHelp   Show this help message

FEATURES:
    • 🤖 Complete bot management (8+ bot types)
    • 📊 Integrated visualizer (API + Frontend)
    • 🔧 Professional process management
    • 🌐 One-click browser integration
    • ⚡ Smart port conflict resolution

PORTS:
    • Engine: http://localhost:5000
    • Visualizer API: http://localhost:5008  
    • Visualizer Frontend: http://localhost:5252

EXAMPLES:
    .\rui-run-frontend.ps1              # Run in Debug mode
    .\rui-run-frontend.ps1 -Release     # Run in Release mode
    .\rui-run-frontend.ps1 -Clean       # Clean build first
"@ -ForegroundColor Cyan
    exit 0
}

# Set working directory to script location
Set-Location (Split-Path -Parent $MyInvocation.MyCommand.Definition)

# Determine configuration
$Configuration = if ($Release) { "Release" } else { "Debug" }

Write-Host "🚀 Starting Zooscape Runner - Professional Bot Manager" -ForegroundColor Green
Write-Host "📁 Working Directory: $(Get-Location)" -ForegroundColor DarkCyan
Write-Host "⚙️  Configuration: $Configuration" -ForegroundColor DarkCyan

# Extend console buffer to prevent line wrapping
try {
    $newWidth = 500
    $bufferSize = $Host.UI.RawUI.BufferSize
    if ($bufferSize.Width -lt $newWidth) {
        $Host.UI.RawUI.BufferSize = New-Object System.Management.Automation.Host.Size($newWidth, $bufferSize.Height)
        Write-Host "🖥️  Extended terminal buffer width to $newWidth to prevent line wrapping." -ForegroundColor DarkCyan
    }
} catch {
    Write-Warning "Could not set terminal buffer width. Output might wrap. Error: $_"
}

# Check if project exists
$ProjectPath = "ZooscapeRunner\ZooscapeRunner\ZooscapeRunner.csproj"
if (-not (Test-Path $ProjectPath)) {
    Write-Error "❌ ZooscapeRunner project not found at: $ProjectPath"
    Write-Host "📁 Current directory contents:" -ForegroundColor Yellow
    Get-ChildItem | Format-Table Name, LastWriteTime -AutoSize
    exit 1
}

Write-Host "✅ Found ZooscapeRunner project: $ProjectPath" -ForegroundColor Green

# Function to run dotnet command with proper error handling
function Invoke-DotNetCommand {
    param(
        [string]$Command,
        [string]$Description
    )
    
    Write-Host "🔄 $Description..." -ForegroundColor Yellow
    Write-Host "   Command: dotnet $Command" -ForegroundColor DarkGray
    
    try {
        $process = Start-Process -FilePath "dotnet" -ArgumentList $Command -NoNewWindow -Wait -PassThru
        
        if ($process.ExitCode -eq 0) {
            Write-Host "✅ $Description completed successfully" -ForegroundColor Green
            return $true
        } else {
            Write-Host "❌ $Description failed with exit code: $($process.ExitCode)" -ForegroundColor Red
            return $false
        }
    } catch {
        Write-Host "❌ $Description failed with exception: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

try {
    # Clean build if requested
    if ($Clean) {
        Write-Host "🧹 Cleaning solution..." -ForegroundColor Yellow
        if (-not (Invoke-DotNetCommand "clean ZooscapeRunner.sln --configuration $Configuration" "Clean")) {
            throw "Clean operation failed"
        }
    }

    # Restore packages
    Write-Host "📦 Restoring NuGet packages..." -ForegroundColor Yellow
    if (-not (Invoke-DotNetCommand "restore ZooscapeRunner.sln" "Package restore")) {
        throw "Package restore failed"
    }

    # Build the application
    Write-Host "🔨 Building ZooscapeRunner..." -ForegroundColor Yellow
    if (-not (Invoke-DotNetCommand "build ZooscapeRunner.sln --configuration $Configuration --no-restore" "Build")) {
        throw "Build failed"
    }

    Write-Host @"

🎉 Build completed successfully!
🚀 Starting ZooscapeRunner GUI Application...

FEATURES AVAILABLE:
• 🤖 Bot Management: Start/Stop/Restart all bots
• 📊 Visualizer: Integrated API + Frontend management  
• 🌐 Browser Integration: One-click visualizer access
• 📋 Real-time Logs: Monitor all processes
• ⚡ Smart Port Management: Automatic conflict resolution

"@ -ForegroundColor Green

    # Run the application
    Write-Host "▶️  Launching application..." -ForegroundColor Cyan
    
    # Use Start-Process to run the GUI application without blocking the PowerShell window
    $AppPath = "ZooscapeRunner\bin\$Configuration\net8.0-windows10.0.19041.0\ZooscapeRunner.exe"
    
    if (Test-Path $AppPath) {
        Write-Host "✅ Found application executable: $AppPath" -ForegroundColor Green
        Write-Host "🚀 Starting GUI application..." -ForegroundColor Green
        
        # Start the GUI application in a separate process
        Start-Process -FilePath $AppPath -WorkingDirectory (Get-Location)
        
        Write-Host @"

✅ ZooscapeRunner GUI Application launched successfully!

🎯 QUICK START GUIDE:
1. Click "▶️ Start All Bots" to launch the bot ecosystem
2. Click "▶️ Start Visualizer" to launch the visualization stack  
3. Click "🌐 Open in Browser" to view the visualizer frontend
4. Monitor real-time logs in the application interface

📊 PORTS:
• Engine: http://localhost:5000
• Visualizer API: http://localhost:5008
• Visualizer Frontend: http://localhost:5252

🔧 The application window should now be open.
   This PowerShell window can be safely closed.

"@ -ForegroundColor Green
        
    } else {
        # Fallback: run with dotnet run
        Write-Host "⚠️  Executable not found, using 'dotnet run' instead..." -ForegroundColor Yellow
        Write-Host "🚀 Starting with dotnet run..." -ForegroundColor Cyan
        
        Set-Location "ZooscapeRunner"
        
        # Use dotnet run for development
        Write-Host "▶️  Running: dotnet run --project ZooscapeRunner.csproj --configuration $Configuration" -ForegroundColor DarkGray
        & dotnet run --project "ZooscapeRunner.csproj" --configuration $Configuration
    }

} catch {
    Write-Host @"

❌ ERROR: Failed to start ZooscapeRunner
   $($_.Exception.Message)

🔧 TROUBLESHOOTING:
1. Ensure .NET 8.0 SDK is installed
2. Check that all NuGet packages are restored
3. Verify project builds successfully
4. Try running with -Clean parameter

💡 For help: .\rui-run-frontend.ps1 -ShowHelp

"@ -ForegroundColor Red
    
    exit 1
} finally {
    Write-Host "🏁 Script execution completed." -ForegroundColor DarkCyan
}