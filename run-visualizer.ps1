# Script to run the Zooscape Visualizer API and Frontend in the same window

Set-Location 'c:\dev\2025-Zooscape\'

# Function to stop processes on a specific port
function Stop-ProcessOnPort {
    param(
        [Parameter(Mandatory=$true)]
        [int]$Port,
        [string]$ServiceName
    )
    
    Write-Host "Checking for processes using port $Port ($ServiceName)..."
    $processInfo = netstat -ano | Select-String ":$Port" | Select-String "LISTENING"
    if ($processInfo) {
        $processId = ($processInfo -split ' ')[-1]
        if ($processId -match '^\d+$') {
            Write-Host "Stopping process with PID $processId using port $Port..."
            Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
            Write-Host "Process stopped."
        }
    }
}

# Stop existing processes
Stop-ProcessOnPort -Port 5252 -ServiceName "Frontend"
Stop-ProcessOnPort -Port 5008 -ServiceName "API"

Write-Host "Starting Zooscape Visualizer API and Frontend..."

$ScriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ApiDir = Join-Path $ScriptDirectory "visualizer-2d\api"
$FrontendDir = Join-Path $ScriptDirectory "visualizer-2d"

# Check if API directory exists
if (-not (Test-Path $ApiDir)) {
    Write-Error "API directory not found: $ApiDir. Please ensure the script is in the root of the Zooscape project."
    exit 1
}

# Check if server.js exists in API directory
if (-not (Test-Path (Join-Path $ApiDir "server.js"))) {
    Write-Error "server.js not found in $ApiDir."
    exit 1
}

# Check if Frontend directory exists
if (-not (Test-Path $FrontendDir)) {
    Write-Error "Frontend directory not found: $FrontendDir. Please ensure the script is in the root of the Zooscape project."
    exit 1
}

# Check if package.json exists in Frontend directory (indicator for npm projects)
if (-not (Test-Path (Join-Path $FrontendDir "package.json"))) {
    Write-Error "package.json not found in $FrontendDir. Cannot start frontend."
    exit 1
}

# Start API in background job
Write-Host "Starting API server on port 5008..."
Push-Location $ApiDir
try {
    Write-Host "Installing API dependencies..."
    npm install
    
    # Start API as a background job
    $apiJob = Start-Job -ScriptBlock {
        Set-Location $using:ApiDir
        node server.js
    }
    Write-Host "API server started as job $($apiJob.Id)"
} finally {
    Pop-Location
}

# Start Frontend in background job
Write-Host "Starting Frontend on port 5252..."
Push-Location $FrontendDir
try {
    Write-Host "Installing Frontend dependencies..."
    npm install
    
    # Start Frontend as a background job
    $frontendJob = Start-Job -ScriptBlock {
        Set-Location $using:FrontendDir
        $env:PORT = "5252"
        npm run dev
    }
    Write-Host "Frontend started as job $($frontendJob.Id)"
} finally {
    Pop-Location
}

Write-Host "Both services are now running in background jobs."
Write-Host "API server (Job $($apiJob.Id)) is running on http://localhost:5008"
Write-Host "Frontend (Job $($frontendJob.Id)) is running on http://localhost:5252"
Write-Host "Press Ctrl+C to stop all services."

# Display job output in real-time
try {
    while ($true) {
        $apiOutput = Receive-Job -Job $apiJob
        $frontendOutput = Receive-Job -Job $frontendJob
        
        if ($apiOutput) {
            Write-Host "[API] $apiOutput" -ForegroundColor Cyan
        }
        
        if ($frontendOutput) {
            Write-Host "[Frontend] $frontendOutput" -ForegroundColor Green
        }
        
        Start-Sleep -Milliseconds 500
    }
} finally {
    # Clean up jobs when script is interrupted
    Stop-Job -Job $apiJob, $frontendJob
    Remove-Job -Job $apiJob, $frontendJob
    Write-Host "All services stopped." -ForegroundColor Yellow
}
