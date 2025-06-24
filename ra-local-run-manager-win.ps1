#!/usr/bin/env pwsh
# PowerShell script to run ZooscapeRunner Windows App
# Usage: .\ra-local-run-manager-win.ps1

Write-Host "Starting ZooscapeRunner Windows App..." -ForegroundColor Green

# Get the script directory and navigate to ZooscapeRunner project
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Join-Path $scriptDir "ZooscapeRunner\ZooscapeRunner"

# Check if project directory exists
if (-not (Test-Path $projectDir)) {
    Write-Host "Error: ZooscapeRunner project directory not found at: $projectDir" -ForegroundColor Red
    Write-Host "Make sure you're running this script from the repository root." -ForegroundColor Red
    exit 1
}

# Check if project file exists
$projectFile = Join-Path $projectDir "ZooscapeRunner.csproj"
if (-not (Test-Path $projectFile)) {
    Write-Host "Error: ZooscapeRunner.csproj not found at: $projectFile" -ForegroundColor Red
    exit 1
}

Write-Host "Project found at: $projectDir" -ForegroundColor Cyan
Write-Host "Running ZooscapeRunner with framework: net8.0-windows10.0.19041.0" -ForegroundColor Cyan

# Change to project directory and run
Set-Location $projectDir

try {
    # Run the application with the specific Windows framework
    dotnet run --framework net8.0-windows10.0.19041.0
}
catch {
    Write-Host "Error running ZooscapeRunner: $_" -ForegroundColor Red
    exit 1
}
finally {
    # Return to original directory
    Set-Location $scriptDir
} 