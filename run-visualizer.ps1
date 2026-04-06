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

function Test-PortListening {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Port
    )

    return $null -ne (netstat -ano | Select-String ":$Port" | Select-String "LISTENING")
}

function Ensure-NpmDependencies {
    param(
        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,
        [Parameter(Mandatory = $true)]
        [string]$NpmCommand
    )

    $nodeModulesPath = Join-Path $WorkingDirectory "node_modules"
    $needsInstall = -not (Test-Path $nodeModulesPath)

    if ($needsInstall) {
        Write-Host "Installing frontend workspace dependencies..." -ForegroundColor Yellow
        Push-Location $WorkingDirectory
        try {
            & $NpmCommand install --no-fund --no-audit
        } finally {
            Pop-Location
        }
    } else {
        Write-Host "Using existing frontend workspace dependencies." -ForegroundColor DarkGray
    }
}

function Show-JobOutput {
    param(
        [Parameter(Mandatory = $true)]
        [System.Management.Automation.Job]$Job,
        [switch]$Keep
    )

    $receiveParams = @{
        Job = $Job
        ErrorAction = 'SilentlyContinue'
    }
    if ($Keep) {
        $receiveParams.Keep = $true
    }

    $jobOutput = Receive-Job @receiveParams
    if ($jobOutput) {
        $color = if ($Job.Name -eq "ZooscapeVisualizerApi") { "Cyan" } else { "Green" }
        foreach ($line in $jobOutput) {
            Write-Host "[$($Job.Name)] $line" -ForegroundColor $color
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
$NodeCommand = (Get-Command node.exe -ErrorAction Stop).Source
$NpmCommand = (Get-Command npm.cmd -ErrorAction Stop).Source
$apiJob = $null
$frontendJob = $null

# Check if API directory exists
if (-not (Test-Path $ApiDir)) {
    Write-Error "API directory not found: $ApiDir. Please ensure the script is in the root of the Zooscape project."
    exit 1
}

# Check if server.cjs exists in API directory
if (-not (Test-Path (Join-Path $ApiDir "server.cjs"))) {
    Write-Error "server.cjs not found in $ApiDir."
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

Ensure-NpmDependencies -WorkingDirectory $FrontendDir -NpmCommand $NpmCommand

# Start API in background job
Write-Host "Starting API server on port 5008..."
$apiJob = Start-Job -Name "ZooscapeVisualizerApi" -ScriptBlock {
    Set-Location $using:ApiDir
    $env:PORT = "5008"
    & $using:NodeCommand server.cjs
}
Write-Host "API server started as job $($apiJob.Id)"

# Start Frontend in background job
Write-Host "Starting Frontend on port 5252..."
$frontendJob = Start-Job -Name "ZooscapeVisualizerFrontend" -ScriptBlock {
    Set-Location $using:FrontendDir
    $env:PORT = "5252"
    & $using:NpmCommand run dev
}
Write-Host "Frontend started as job $($frontendJob.Id)"

$startupDeadline = (Get-Date).AddSeconds(20)
while ((Get-Date) -lt $startupDeadline) {
    Show-JobOutput -Job $apiJob
    Show-JobOutput -Job $frontendJob

    $failedJobs = @($apiJob, $frontendJob) | Where-Object { $_.State -in @('Failed', 'Stopped', 'Completed') }
    if ($failedJobs) {
        foreach ($job in $failedJobs) {
            Write-Host "Job '$($job.Name)' exited early with state $($job.State)." -ForegroundColor Red
            Show-JobOutput -Job $job -Keep
        }

        throw "One or more visualizer services failed during startup."
    }

    if ((Test-PortListening -Port 5008) -and (Test-PortListening -Port 5252)) {
        break
    }

    Start-Sleep -Milliseconds 500
}

if (-not ((Test-PortListening -Port 5008) -and (Test-PortListening -Port 5252))) {
    Show-JobOutput -Job $apiJob -Keep
    Show-JobOutput -Job $frontendJob -Keep
    throw "Timed out waiting for the visualizer services to listen on ports 5008 and 5252."
}

Write-Host "Both services are now running in background jobs."
Write-Host "API server (Job $($apiJob.Id)) is running on http://localhost:5008"
Write-Host "Frontend (Job $($frontendJob.Id)) is running on http://localhost:5252"
Write-Host "Press Ctrl+C to stop all services."

# Display job output in real-time
try {
    while ($true) {
        Show-JobOutput -Job $apiJob
        Show-JobOutput -Job $frontendJob

        if ($apiJob.State -notin @('Running', 'NotStarted') -or $frontendJob.State -notin @('Running', 'NotStarted')) {
            Write-Host "A visualizer job exited with API state '$($apiJob.State)' and Frontend state '$($frontendJob.State)'." -ForegroundColor Yellow
            break
        }
        
        Start-Sleep -Milliseconds 500
    }
} finally {
    # Clean up jobs when script is interrupted
    if ($apiJob) {
        Stop-Job -Job $apiJob -ErrorAction SilentlyContinue
        Remove-Job -Job $apiJob -ErrorAction SilentlyContinue
    }
    if ($frontendJob) {
        Stop-Job -Job $frontendJob -ErrorAction SilentlyContinue
        Remove-Job -Job $frontendJob -ErrorAction SilentlyContinue
    }
    Write-Host "All services stopped." -ForegroundColor Yellow
}
