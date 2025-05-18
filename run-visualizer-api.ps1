# Script to run the Zooscape Visualizer API
param (
    [string]$LogsDir = "C:/dev/2025-Zooscape/logs" # Default logs directory, ensure this path is correct for your system
)

Write-Host "Starting Zooscape Visualizer API..."
Write-Host "Using logs directory: $LogsDir"
Write-Host "API will run on port: 5008"

# Set environment variables for the Node.js process
$env:LOGS_DIR = $LogsDir
$env:PORT = "5008" # Ensure API runs on port 5008 (as string for env var)

$ApiDir = Join-Path $PSScriptRoot "visualizer-2d\api"

if (-not (Test-Path $ApiDir)) {
    Write-Error "API directory not found: $ApiDir. Please ensure the script is in the root of the Zooscape project."
    exit 1
}

if (-not (Test-Path (Join-Path $ApiDir "server.js"))) {
    Write-Error "server.js not found in $ApiDir."
    exit 1
}

Push-Location $ApiDir
Write-Host "Running API from: $(Get-Location)"

# Check if nodemon is available, otherwise use node
If (Get-Command nodemon -ErrorAction SilentlyContinue) {
    Write-Host "Using nodemon to run server.js. Press CTRL+C to stop."
    nodemon server.js
} Else {
    Write-Host "nodemon not found. Using node to run server.js. For auto-restarts during development, consider installing nodemon globally (npm install -g nodemon). Press CTRL+C to stop."
    node server.js
}

Pop-Location

# It's good practice to clear environment variables if they were only meant for this script execution,
# though they are session-specific by default unless explicitly set machine/user-wide.
# Remove-Item Env:\LOGS_DIR
# Remove-Item Env:\PORT

Write-Host "Zooscape Visualizer API has been stopped."
