# Script to run the Zooscape Visualizer API and Frontend in the same window

Set-Location 'c:\dev\2025-Zooscape\'

# Function to stop processes on a specific port
function Stop-ProcessOnPort {
    param(
        [Parameter(Mandatory=$true)]
        [int]$Port,
        [string]$ServiceName
    )
    
    Write-Host "Attempting to stop $ServiceName on port $Port..." -ForegroundColor Cyan
    $processLines = netstat -ano | Select-String ":$Port" | Select-String "LISTENING"
    if ($processLines) {
        $stoppedCount = 0
        foreach ($lineInfo in $processLines) {
            $line = $lineInfo.Line.Trim() -replace '\s+', ' '
            $processId = ($line -split ' ')[-1]
            
            # Validate PID is a number
            if ($processId -match '^\d+$') {
                $processId = [int]$processId
                
                # Check if process exists
                $processExists = Get-Process -Id $processId -ErrorAction SilentlyContinue
                if (-not $processExists) {
                    Write-Host "Process $processId not found, already terminated?" -ForegroundColor Gray
                    continue
                }
                
                Write-Host "Found $ServiceName (PID: $processId) listening on port $Port. Attempting to stop..." -ForegroundColor Yellow
                try {
                    Stop-Process -Id $processId -Force -ErrorAction Stop
                    
                    # Wait for process to exit
                    $maxRetries = 5
                    $retryCount = 0
                    while ($retryCount -lt $maxRetries -and (Get-Process -Id $processId -ErrorAction SilentlyContinue)) {
                        Start-Sleep -Milliseconds 200
                        $retryCount++
                    }
                    
                    if ($retryCount -eq $maxRetries) {
                        Write-Warning "Failed to terminate process $processId after $maxRetries retries"
                    } else {
                        Write-Host "Successfully stopped $ServiceName (PID: $processId) on port $Port." -ForegroundColor Green
                        $stoppedCount++
                    }
                }
                catch {
                    $ExceptionMsg = $_.Exception.Message
                    $WarningMessage = "Failed to stop {0} (PID: {1}) on port {2}: {3}" -f $ServiceName, $processId, $Port, $ExceptionMsg
                    Write-Warning $WarningMessage
                }
            }
            else {
                Write-Warning "Could not parse PID for $ServiceName on port $Port from netstat line: $line"
            }
        }
        
        if ($stoppedCount -gt 0) {
            Write-Host "Stopped $stoppedCount process(es) for $ServiceName on port $Port." -ForegroundColor Green
        }
    }
    else {
        Write-Host "No $ServiceName found listening on port $Port." -ForegroundColor Gray
    }
}

# Stop existing processes
Stop-ProcessOnPort -Port 5252 -ServiceName "Frontend"
Stop-ProcessOnPort -Port 5008 -ServiceName "Visualizer API"

# Wait a moment for ports to be fully released
Write-Host "Waiting for ports to be fully released..." -ForegroundColor Yellow
Start-Sleep -Seconds 3

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
    $apiJob = Start-Job -ScriptBlock {
        Set-Location $using:ApiDir
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
    
    # Wait for ports to be fully released
    Write-Host "Waiting for ports to be fully released..." -ForegroundColor Yellow
    Start-Sleep -Seconds 2

    # Then, stop and remove the PowerShell jobs
    Write-Host "Stopping and removing PowerShell background jobs..." -ForegroundColor Gray
    if ($apiJob) { Stop-Job -Job $apiJob -Force -ErrorAction SilentlyContinue }
    if ($frontendJob) { Stop-Job -Job $frontendJob -Force -ErrorAction SilentlyContinue }
    if ($apiJob) { Remove-Job -Job $apiJob -Force -ErrorAction SilentlyContinue }
    if ($frontendJob) { Remove-Job -Job $frontendJob -Force -ErrorAction SilentlyContinue }

    Write-Host "All services and jobs should now be stopped." -ForegroundColor Yellow
} 