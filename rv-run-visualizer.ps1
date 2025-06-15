# Script to run the Zooscape Visualizer API and Frontend in the same window

Set-Location 'c:\dev\2025-Zooscape\'

# Function to stop processes on a specific port
function Stop-ProcessOnPort {
    param(
        [Parameter(Mandatory=$true)]
        [int]$Port,
        [string]$ServiceName
    )
    
    Write-Host "Attempting to stop $ServiceName on port $Port..."
    $processLines = netstat -ano | Select-String ":$Port" | Select-String "LISTENING"
    if ($processLines) {
        foreach ($lineInfo in $processLines) {
            $line = $lineInfo.Line.Trim() -replace '\s+', ' '
            $processId = ($line -split ' ')[-1]
            if ($processId -match '^\d+$') {
                Write-Host "Found $ServiceName (PID: $processId) listening on port $Port. Attempting to stop..." -ForegroundColor Yellow
                try {
                    Stop-Process -Id $processId -Force -ErrorAction Stop
                    Write-Host "Successfully stopped $ServiceName (PID: $processId) on port $Port." -ForegroundColor Green
                }
                catch {
                    $ErrorRecord = $_ # Assign the error record to a local variable
                    $ExceptionMsg = "[No exception message available]" # Default message
                    if (($ErrorRecord).Exception) {
                        $ExceptionMsg = $ErrorRecord.Exception.Message
                    }
                    $WarningMessage = "Failed to stop {0} (PID: {1}) on port {2}: {3}" -f $ServiceName, $processId, $Port, $ExceptionMsg
                    Write-Warning $WarningMessage
                }
            }
            else {
                Write-Warning "Could not parse PID for $ServiceName on port $Port from netstat line: $line"
            }
        }
    }
    else {
        Write-Host "No $ServiceName found listening on port $Port."
    }
}

# Stop existing processes
Stop-ProcessOnPort -Port 5252 -ServiceName "Frontend"
Stop-ProcessOnPort -Port 5008 -ServiceName "Visualizer API"


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

# Start Visualizer API in background job
Write-Host "Starting Visualizer API server on port 5008..."
Push-Location $ApiDir
try {
    Write-Host "Installing Visualizer API dependencies..."
    npm install
    
    # Start API as a background job with nodemon for hot-reloading
    $apiJob = Start-Job -ScriptBlock {
        Set-Location $using:ApiDir
        # Call nodemon directly from node_modules/.bin
        & ".\node_modules\.bin\nodemon.cmd" --watch ./ server.js
    }
    Write-Host "Visualizer API server started as job $($apiJob.Id)"
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
        vite
    }
    Write-Host "Frontend started as job $($frontendJob.Id)"
} finally {
    Pop-Location
}

Write-Host "All services are now running in background jobs."
Write-Host "Visualizer API (Job $($apiJob.Id)) is running on http://localhost:5008"
Write-Host "Frontend (Job $($frontendJob.Id)) is running on http://localhost:5252"
Write-Host "Press Ctrl+C to stop all services."

# Display job output in real-time
try {
    while ($true) {
        $apiOutput = Receive-Job -Job $apiJob

        $frontendOutput = Receive-Job -Job $frontendJob
        
        if ($apiOutput) {
            Write-Host "[Visualizer API] $apiOutput" -ForegroundColor Cyan
        }
        
        if ($frontendOutput) {
            Write-Host "[Frontend] $frontendOutput" -ForegroundColor Green
        }
        
        Start-Sleep -Milliseconds 500
    }
} finally {
    Write-Host "Interrupt received or script ending. Cleaning up..." -ForegroundColor Magenta

    # Attempt to stop processes by port first for quicker port release and service termination
    Stop-ProcessOnPort -Port 5008 -ServiceName "Visualizer API"
    Stop-ProcessOnPort -Port 5252 -ServiceName "Frontend"

    # Then, stop and remove the PowerShell jobs
    Write-Host "Stopping and removing PowerShell background jobs..." -ForegroundColor Gray
    if ($apiJob) { Stop-Job -Job $apiJob -Force -ErrorAction SilentlyContinue }
    if ($frontendJob) { Stop-Job -Job $frontendJob -Force -ErrorAction SilentlyContinue }
    if ($apiJob) { Remove-Job -Job $apiJob -Force -ErrorAction SilentlyContinue }
    if ($frontendJob) { Remove-Job -Job $frontendJob -Force -ErrorAction SilentlyContinue }

    Write-Host "All services and jobs should now be stopped." -ForegroundColor Yellow
} 