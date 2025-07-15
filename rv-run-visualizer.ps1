# Script to run the Zooscape Visualizer API and Frontend in separate tabs

Set-Location 'c:\dev\2025-Zooscape\'

# Prevent line wrapping by extending the buffer width
try {
    $newWidth = 500
    $bufferSize = $Host.UI.RawUI.BufferSize
    if ($bufferSize.Width -lt $newWidth) {
        $Host.UI.RawUI.BufferSize = New-Object System.Management.Automation.Host.Size($newWidth, $bufferSize.Height)
        Write-Host "Set terminal buffer width to $newWidth to prevent line wrapping." -ForegroundColor DarkCyan
    }
} catch {
    Write-Warning "Could not set terminal buffer width. Output might wrap. Error: $_"
}

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

# Check if Windows Terminal is available
$wtAvailable = $false
try {
    $wtPath = Get-Command wt.exe -ErrorAction Stop
    $wtAvailable = $true
    Write-Host "Windows Terminal found at: $($wtPath.Source)" -ForegroundColor Green
} catch {
    Write-Warning "Windows Terminal (wt.exe) not found. Please install Windows Terminal from the Microsoft Store."
    Write-Host "Falling back to separate PowerShell windows..." -ForegroundColor Yellow
}

# Stop existing processes
Stop-ProcessOnPort -Port 5252 -ServiceName "Frontend"
Stop-ProcessOnPort -Port 5008 -ServiceName "FunctionalTests API"

# Wait a moment for ports to be fully released
Write-Host "Waiting for ports to be fully released..." -ForegroundColor Yellow
Start-Sleep -Seconds 3

Write-Host "DEBUG: Script proceeding after sleep."

if ($wtAvailable) {
    Write-Host "Starting Zooscape Visualizer API and Frontend in separate tabs using Windows Terminal..."
} else {
    Write-Host "Starting Zooscape Visualizer API and Frontend in separate windows..."
}

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

if ($wtAvailable) {
    # Prepare API command
    $apiCommand = @"
Set-Location '$ScriptDirectory'
`$env:ASPNETCORE_URLS = 'http://localhost:5008'
Write-Host "Starting API server on http://localhost:5008 with hot reload..." -ForegroundColor Cyan
dotnet watch run --project '$FunctionalTestsProject' --configuration Release --no-build
Write-Host "API server stopped. Press Enter to close this tab..." -ForegroundColor Red
Read-Host
"@
    $encodedApiCommand = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($apiCommand))

    # Prepare Frontend command
    $frontendCommand = @"
Set-Location '$FrontendDir'
`$env:PORT = '5252'
Write-Host "Starting Frontend server on http://localhost:5252..." -ForegroundColor Green
npm run dev
Write-Host "Frontend server stopped. Press Enter to close this tab..." -ForegroundColor Red
Read-Host
"@
    $encodedFrontendCommand = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($frontendCommand))

    # Build arguments for all tabs
    $allWtArgs = @(
        "--window", "0", # Force a new window instance
        # First Tab: API Server
        "new-tab", 
        "--title", "APIServer", 
        "--tabColor", "#0078d4", 
        "--suppressApplicationTitle",
        "-d", $ScriptDirectory, 
        "--", "pwsh", "-NoExit", "-NoLogo", "-EncodedCommand", $encodedApiCommand,
        
        # Second Tab: Frontend
        ";", "new-tab", 
        "--title", "Frontend", 
        "--tabColor", "#107c10",
        "--suppressApplicationTitle",
        "-d", $FrontendDir, 
        "--", "pwsh", "-NoExit", "-NoLogo", "-EncodedCommand", $encodedFrontendCommand
    )

    # Launch Windows Terminal with all tabs at once
    Write-Host "Starting FunctionalTests API and Frontend in a single Windows Terminal instance..."
    Start-Process wt.exe -ArgumentList $allWtArgs -WindowStyle Minimized
} else {
    # Fallback to separate PowerShell windows using temporary files
    $tempDir = [System.IO.Path]::GetTempPath()
    $apiScriptPath = Join-Path $tempDir "zooscape-api-runner.ps1"
    $frontendScriptPath = Join-Path $tempDir "zooscape-frontend-runner.ps1"

    $apiCommandForFile = @"
    Set-Location '$ScriptDirectory'
    `$env:ASPNETCORE_URLS = 'http://localhost:5008'
    Write-Host "Starting API server on http://localhost:5008 with hot reload..." -ForegroundColor Cyan
    dotnet watch run --project '$FunctionalTestsProject' --configuration Release --no-build
    Write-Host "API server stopped. Press Enter to close this window..." -ForegroundColor Red
    Read-Host
"@
    $apiCommandForFile | Set-Content -Path $apiScriptPath -Encoding UTF8 -Force

    $frontendCommandForFile = @"
    Set-Location '$FrontendDir'
    `$env:PORT = '5252'
    Write-Host "Starting Frontend server on http://localhost:5252..." -ForegroundColor Green
    npm run dev
    Write-Host "Frontend server stopped. Press Enter to close this window..." -ForegroundColor Red
    Read-Host
"@
    $frontendCommandForFile | Set-Content -Path $frontendScriptPath -Encoding UTF8 -Force
    
    Write-Host "Starting FunctionalTests API server (with Leaderboard) on port 5008 in new window..."
    Start-Process powershell.exe -ArgumentList "-NoExit", "-File", $apiScriptPath -WindowStyle Minimized

    Write-Host "Starting Frontend server in new window..."
    Start-Process powershell.exe -ArgumentList "-NoExit", "-File", $frontendScriptPath -WindowStyle Minimized
}

# Give the services a moment to start
Start-Sleep -Seconds 2

Write-Host "All services are now running in separate tabs." -ForegroundColor Green
Write-Host "FunctionalTests API with Leaderboard is starting on http://localhost:5008" -ForegroundColor Cyan
Write-Host "Visualizer Frontend is starting on http://localhost:5252" -ForegroundColor Green
Write-Host ""
if ($wtAvailable) {
    Write-Host "Services are running in separate Windows Terminal tabs with colors:" -ForegroundColor White
    Write-Host "  [BLUE] API Server (Blue tab) - http://localhost:5008" -ForegroundColor Cyan
    Write-Host "  [GREEN] Frontend (Green tab) - http://localhost:5252" -ForegroundColor Green
    Write-Host "Close those tabs or press Ctrl+C in them to stop the services."
} else {
    Write-Host "Services are running in separate PowerShell windows."
    Write-Host "Close those windows or press Ctrl+C in them to stop the services."
}
Write-Host ""
Write-Host "Press Ctrl+C here to stop monitoring and cleanup any remaining processes."

# Simple monitoring loop that allows cleanup on Ctrl+C
try {
    while ($true) {
        Start-Sleep -Seconds 5
        
        # Check if services are still running
        $apiRunning = $null -ne (netstat -ano | Select-String ":5008" | Select-String "LISTENING")
        $frontendRunning = $null -ne (netstat -ano | Select-String ":5252" | Select-String "LISTENING")
        
        if (-not $apiRunning -and -not $frontendRunning) {
            Write-Host "Both services appear to have stopped. Exiting monitoring." -ForegroundColor Yellow
            break
        }
    }
}
finally {
    Write-Host "Interrupt received or script ending. Cleaning up any remaining processes..." -ForegroundColor Magenta

    # Attempt to stop processes by port as a final cleanup
    Stop-ProcessOnPort -Port 5008 -ServiceName "FunctionalTests API"
    Stop-ProcessOnPort -Port 5252 -ServiceName "Visualizer Frontend"

    # Cleanup temporary scripts if they exist (for the fallback case)
    $apiScriptPath = Join-Path ([System.IO.Path]::GetTempPath()) "zooscape-api-runner.ps1"
    $frontendScriptPath = Join-Path ([System.IO.Path]::GetTempPath()) "zooscape-frontend-runner.ps1"
    if (Test-Path $apiScriptPath) {
        Remove-Item $apiScriptPath -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path $frontendScriptPath) {
        Remove-Item $frontendScriptPath -Force -ErrorAction SilentlyContinue
    }

    if ($wtAvailable) {
        Write-Host "Cleanup completed. You may need to manually close any remaining Windows Terminal tabs." -ForegroundColor Yellow
    } else {
        Write-Host "Cleanup completed. You may need to manually close any remaining PowerShell windows." -ForegroundColor Yellow
    }
} 