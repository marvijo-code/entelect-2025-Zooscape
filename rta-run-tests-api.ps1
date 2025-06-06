# Script to run the Zooscape FunctionalTests API

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

# Stop existing FunctionalTests API process
Stop-ProcessOnPort -Port 5009 -ServiceName "FunctionalTests API"

Write-Host "Starting Zooscape FunctionalTests API..."

$ScriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Definition
$FunctionalTestsDir = Join-Path $ScriptDirectory "FunctionalTests"

# Check if FunctionalTests directory exists
if (-not (Test-Path $FunctionalTestsDir)) {
    Write-Error "FunctionalTests directory not found: $FunctionalTestsDir. Please ensure the script is in the root of the Zooscape project."
    exit 1
}

# Check if FunctionalTests.csproj exists
if (-not (Test-Path (Join-Path $FunctionalTestsDir "FunctionalTests.csproj"))) {
    Write-Error "FunctionalTests.csproj not found in $FunctionalTestsDir. Cannot start FunctionalTests API."
    exit 1
}

# Start FunctionalTests API in background job
Write-Host "Starting FunctionalTests API server on port 5009..."
Push-Location $FunctionalTestsDir
$functionalTestsJob = $null
try {
    Write-Host "Building FunctionalTests API..."
    dotnet build --verbosity quiet
    
    # Start FunctionalTests API as a background job
    $functionalTestsJob = Start-Job -ScriptBlock {
        Set-Location $using:FunctionalTestsDir
        dotnet run --urls "http://localhost:5009"
    }
    Write-Host "FunctionalTests API started as job $($functionalTestsJob.Id)"
} finally {
    Pop-Location
}

Write-Host "FunctionalTests API (Job $($functionalTestsJob.Id)) is running on http://localhost:5009"
Write-Host "Press Ctrl+C to stop the service."

# Display job output in real-time
try {
    while ($functionalTestsJob -and $functionalTestsJob.State -eq 'Running') {
        $functionalTestsOutput = Receive-Job -Job $functionalTestsJob
        
        if ($functionalTestsOutput) {
            Write-Host "[FunctionalTests API] $functionalTestsOutput" -ForegroundColor Magenta
        }
        
        Start-Sleep -Milliseconds 500
    }
} finally {
    # Clean up job when script is interrupted or job completes
    if ($functionalTestsJob) {
        Write-Host "Stopping FunctionalTests API job..." -ForegroundColor Yellow
        Stop-Job -Job $functionalTestsJob
        Remove-Job -Job $functionalTestsJob
        Write-Host "FunctionalTests API service stopped." -ForegroundColor Yellow
    }
}
