# Set environment variables
$env:HUSKY = "0"
$env:ASPNETCORE_URLS = "http://localhost:5000"

# Create logs directory if it doesn't exist
$logsPath = Join-Path $PSScriptRoot "logs"
if (-not (Test-Path $logsPath)) {
    New-Item -ItemType Directory -Path $logsPath | Out-Null
    Write-Host "📁 Created logs directory at: $logsPath" -ForegroundColor Yellow
}
$env:LOG_DIR = $logsPath

Write-Host "🎮 Building Zooscape in Release configuration..." -ForegroundColor Cyan

# Restore dependencies
dotnet restore ./Zooscape/Zooscape.csproj

# Build the project
dotnet build ./Zooscape/Zooscape.csproj -c Release

Write-Host "🚀 Starting Zooscape server..." -ForegroundColor Green

# Run the application
dotnet run --project ./Zooscape/Zooscape.csproj -c Release --no-build

# Note: The server will be available at:
# - Main endpoint: http://localhost:5000
# - Health check: http://localhost:5000/bothub 