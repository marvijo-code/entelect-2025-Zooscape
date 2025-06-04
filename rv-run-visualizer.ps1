# Script to run the Zooscape Visualizer API, Frontend, and FunctionalTests API in the same window

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
Stop-ProcessOnPort -Port 5008 -ServiceName "Visualizer API"
Stop-ProcessOnPort -Port 5009 -ServiceName "FunctionalTests API"

Write-Host "Starting Zooscape Visualizer API, Frontend, and FunctionalTests API..."

$ScriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ApiDir = Join-Path $ScriptDirectory "visualizer-2d\api"
$FrontendDir = Join-Path $ScriptDirectory "visualizer-2d"
$FunctionalTestsDir = Join-Path $ScriptDirectory "FunctionalTests"

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

# Start FunctionalTests API in background job
Write-Host "Starting FunctionalTests API server on port 5009..."
Push-Location $FunctionalTestsDir
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
Write-Host "FunctionalTests API (Job $($functionalTestsJob.Id)) is running on http://localhost:5009"
Write-Host "Frontend (Job $($frontendJob.Id)) is running on http://localhost:5252"
Write-Host "Press Ctrl+C to stop all services."

# Display job output in real-time
try {
    while ($true) {
        $apiOutput = Receive-Job -Job $apiJob
        $functionalTestsOutput = Receive-Job -Job $functionalTestsJob
        $frontendOutput = Receive-Job -Job $frontendJob
        
        if ($apiOutput) {
            Write-Host "[Visualizer API] $apiOutput" -ForegroundColor Cyan
        }
        
        if ($functionalTestsOutput) {
            Write-Host "[FunctionalTests API] $functionalTestsOutput" -ForegroundColor Magenta
        }
        
        if ($frontendOutput) {
            Write-Host "[Frontend] $frontendOutput" -ForegroundColor Green
        }
        
        Start-Sleep -Milliseconds 500
    }
} finally {
    # Clean up jobs when script is interrupted
    Stop-Job -Job $apiJob, $functionalTestsJob, $frontendJob
    Remove-Job -Job $apiJob, $functionalTestsJob, $frontendJob
    Write-Host "All services stopped." -ForegroundColor Yellow
} 