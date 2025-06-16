# Script to run the Zooscape Visualizer API and Frontend in the same window

Set-Location 'c:\dev\2025-Zooscape\'

# Function to stop processes on a specific port
function Stop-ProcessOnPort {
    param(
        [Parameter(Mandatory = $true)]
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
                    }
                    else {
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

Write-Host "DEBUG: Script proceeding after sleep."

Write-Host "Starting Zooscape Visualizer API and Frontend..."

$ScriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Definition
$FunctionalTestsProject = Join-Path $ScriptDirectory "FunctionalTests\FunctionalTests.csproj"
$FrontendDir = Join-Path $ScriptDirectory "visualizer-2d"

# Check if FunctionalTests project exists
if (-not (Test-Path $FunctionalTestsProject)) {
    Write-Error "FunctionalTests project not found: $FunctionalTestsProject. Please ensure the script is in the root of the Zooscape project."
    exit 1
}

# Check if Frontend directory exists
if (-not (Test-Path $FrontendDir)) {
    Write-Error "Frontend directory not found: $FrontendDir. Please ensure the script is in the root of the Zooscape project."
    exit 1
}

# Start API server in a background job
Write-Host "Starting .NET API server..."
$apiJob = Start-Job -ScriptBlock {
    param($scriptDir, $project)
    Set-Location $scriptDir
    dotnet watch --project $project
} -ArgumentList $ScriptDirectory, $FunctionalTestsProject

# Start Frontend in background job
Write-Host "Starting Frontend server..."
$frontendJob = Start-Job -ScriptBlock {
    param($dir)
    Set-Location $dir
    # Vite needs an interactive console to pick up on port changes, but this should work for default.
    # We set the port via env var to be sure.
    $env:PORT = "5252"
    npm run dev
} -ArgumentList $FrontendDir

# Verify jobs started
if (-not $apiJob) {
    Write-Error "Failed to start the API server job."
    exit 1
}
if (-not $frontendJob) {
    Write-Error "Failed to start the frontend server job."
    if ($apiJob) { Stop-Job $apiJob -Force } # Clean up the other job
    exit 1
}

Write-Host "All services are now running in background jobs." -ForegroundColor Green
Write-Host ".NET API (Job $($apiJob.Id)) is starting on http://localhost:5008"
Write-Host "Frontend (Job $($frontendJob.Id)) is starting on http://localhost:5252"
Write-Host "Press Ctrl+C to stop all services."

# Display job output in real-time
try {
    while ($true) {
        $jobs = @($apiJob, $frontendJob)
        $jobs | ForEach-Object {
            if ($_.State -eq 'Failed') {
                Write-Error "Job $($_.Id) - $($_.Name) has failed."
                Receive-Job $_ # To get the error message
            }
        }

        $apiOutput = Receive-Job -Job $apiJob -ErrorAction SilentlyContinue
        if ($apiOutput) {
            Write-Host "[API - .NET] $apiOutput" -ForegroundColor Cyan
        }

        $frontendOutput = Receive-Job -Job $frontendJob -ErrorAction SilentlyContinue
        if ($frontendOutput) {
            Write-Host "[Frontend] $frontendOutput" -ForegroundColor Green
        }
        
        # If both jobs are completed/failed/stopped, exit the loop
        if (($apiJob.State -ne 'Running') -and ($frontendJob.State -ne 'Running')) {
            Write-Warning "Both jobs have stopped. Exiting monitoring loop."
            break
        }

        Start-Sleep -Milliseconds 500
    }
}
finally {
    Write-Host "Interrupt received or script ending. Cleaning up..." -ForegroundColor Magenta

    # Stop and remove the PowerShell jobs
    Write-Host "Stopping and removing PowerShell background jobs..."
    Get-Job | Stop-Job -Force -ErrorAction SilentlyContinue
    Get-Job | Remove-Job -Force -ErrorAction SilentlyContinue

    # Attempt to stop processes by port as a final cleanup
    Stop-ProcessOnPort -Port 5008 -ServiceName "API"
    Stop-ProcessOnPort -Port 5252 -ServiceName "Frontend"

    Write-Host "All services and jobs should now be stopped." -ForegroundColor Yellow
} 