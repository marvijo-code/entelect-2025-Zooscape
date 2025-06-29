#!/usr/bin/env pwsh
# Quick Start All - Direct launch of all components
# This bypasses ZooscapeRunner and starts everything directly

Write-Host "🚀 Quick Start All - Zooscape Bot Ecosystem" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Green

# Kill any existing processes on our ports
Write-Host "🧹 Cleaning up existing processes..." -ForegroundColor Yellow
Get-Process | Where-Object {$_.ProcessName -like "*dotnet*" -or $_.ProcessName -like "*node*"} | Stop-Process -Force -ErrorAction SilentlyContinue

Start-Sleep 2

# Start Zooscape Engine (Main Game Server)
Write-Host "🎮 Starting Zooscape Engine on port 5000..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot'; dotnet run --project engine/Zooscape/Zooscape.csproj --urls=http://localhost:5000"

Start-Sleep 3

# Start Visualizer API 
Write-Host "📊 Starting Visualizer API on port 5008..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot'; dotnet run --project Zooscape.API/Zooscape.API.csproj --urls=http://localhost:5008"

Start-Sleep 2

# Start Visualizer Frontend
Write-Host "🌐 Starting Visualizer Frontend on port 5252..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot/visualizer-2d'; npm run dev"

Start-Sleep 2

# Start Bots
Write-Host "🤖 Starting Bots..." -ForegroundColor Cyan

$bots = @(
    @{Name="ClingyHeuroBot2"; Path="Bots/ClingyHeuroBot2/ClingyHeuroBot2.csproj"; Token=[guid]::NewGuid().ToString()}
    @{Name="ClingyHeuroBotExp"; Path="Bots/ClingyHeuroBotExp/ClingyHeuroBotExp.csproj"; Token=[guid]::NewGuid().ToString()}
    @{Name="ClingyHeuroBot"; Path="Bots/ClingyHeuroBot/ClingyHeuroBot.csproj"; Token=[guid]::NewGuid().ToString()}
    @{Name="DeepMCTS"; Path="Bots/DeepMCTS/DeepMCTS.csproj"; Token=[guid]::NewGuid().ToString()}
    @{Name="MCTSo4"; Path="Bots/MCTSo4/MCTSo4.csproj"; Token=[guid]::NewGuid().ToString()}
    @{Name="ReferenceBot"; Path="Bots/ReferenceBot/ReferenceBot.csproj"; Token=[guid]::NewGuid().ToString()}
)

foreach ($bot in $bots) {
    Write-Host "  🤖 Starting $($bot.Name)..." -ForegroundColor Yellow
    $env:BOT_NICKNAME = $bot.Name
    $env:BOT_TOKEN = $bot.Token
    $env:Token = $bot.Token
    
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "`$env:BOT_NICKNAME='$($bot.Name)'; `$env:BOT_TOKEN='$($bot.Token)'; `$env:Token='$($bot.Token)'; cd '$PSScriptRoot'; dotnet run --project $($bot.Path)"
    Start-Sleep 1
}

# Start AdvancedMCTSBot (C++)
Write-Host "  🤖 Starting AdvancedMCTSBot (C++)..." -ForegroundColor Yellow
$env:BOT_NICKNAME = "AdvancedMCTSBot"
$env:BOT_TOKEN = [guid]::NewGuid().ToString()
$env:Token = $env:BOT_TOKEN
Start-Process powershell -ArgumentList "-NoExit", "-Command", "`$env:BOT_NICKNAME='AdvancedMCTSBot'; `$env:BOT_TOKEN='$($env:BOT_TOKEN)'; `$env:Token='$($env:BOT_TOKEN)'; cd '$PSScriptRoot/Bots/AdvancedMCTSBot'; build.bat; build/Release/AdvancedMCTSBot.exe"

Write-Host ""
Write-Host "✅ All services started!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Service URLs:" -ForegroundColor White
Write-Host "  🎮 Game Engine: http://localhost:5000" -ForegroundColor Gray
Write-Host "  📊 Visualizer API: http://localhost:5008" -ForegroundColor Gray  
Write-Host "  🌐 Visualizer UI: http://localhost:5252" -ForegroundColor Gray
Write-Host ""
Write-Host "💡 Check each PowerShell window for logs" -ForegroundColor Yellow
Write-Host "🔄 Press Ctrl+C in each window to stop services" -ForegroundColor Yellow 