#!/usr/bin/env pwsh
# PowerShell script to run the AdvancedMCTSBot

Write-Host "=== Starting AdvancedMCTSBot ===" -ForegroundColor Green
Write-Host "Navigating to AdvancedMCTSBot directory..." -ForegroundColor Yellow

# Change to the AdvancedMCTSBot directory
Set-Location "Bots\AdvancedMCTSBot"

# Check if the executable exists
if (Test-Path ".\build\Release\AdvancedMCTSBot.exe") {
    Write-Host "Found AdvancedMCTSBot.exe, starting bot..." -ForegroundColor Green
    
    # Run the bot executable
    .\build\Release\AdvancedMCTSBot.exe
}
else {
    Write-Host "ERROR: AdvancedMCTSBot.exe not found!" -ForegroundColor Red
    Write-Host "Please ensure the bot has been built successfully." -ForegroundColor Red
    Write-Host "Expected location: .\build\Release\AdvancedMCTSBot.exe" -ForegroundColor Yellow
}

Write-Host "=== AdvancedMCTSBot session ended ===" -ForegroundColor Green