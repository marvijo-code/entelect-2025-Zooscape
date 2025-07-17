#!/usr/bin/env pwsh

# Script to analyze StaticHeuro bot's pellet collection behavior
# Identifies ticks where the bot doesn't take a pellet when one is available

param(
    [string]$LogDirectory = "logs\20250717_155412",
    [string]$BotName = "StaticHeuro",
    [int]$StartTick = 1,
    [int]$EndTick = 100
)

Write-Host "=== ANALYZING PELLET COLLECTION BEHAVIOR ===" -ForegroundColor Green
Write-Host "Bot: $BotName" -ForegroundColor Yellow
Write-Host "Log Directory: $LogDirectory" -ForegroundColor Yellow
Write-Host "Analyzing ticks $StartTick to $EndTick" -ForegroundColor Yellow
Write-Host ""

$missedPelletTicks = @()
$totalTicks = 0
$ticksWithAdjacentPellets = 0

for ($tick = $StartTick; $tick -le $EndTick; $tick++) {
    $jsonFile = "$LogDirectory\$tick.json"
    
    if (Test-Path $jsonFile) {
        $totalTicks++
        
        # Run GameStateInspector to analyze this tick
        $output = & dotnet run --project tools\GameStateInspector -- $jsonFile $BotName 2>$null
        
        if ($output -match "Pellet Up\? Yes|Pellet Left\? Yes|Pellet Right\? Yes|Pellet Down\? Yes") {
            $ticksWithAdjacentPellets++
            
            # Check if bot's position changed in next tick (indicating movement)
            $nextTick = $tick + 1
            $nextJsonFile = "$LogDirectory\$nextTick.json"
            
            if (Test-Path $nextJsonFile) {
                $currentOutput = $output
                $nextOutput = & dotnet run --project tools\GameStateInspector -- $nextJsonFile $BotName 2>$null
                
                # Extract positions
                $currentPos = if ($currentOutput -match "Bot Position: \((\d+), (\d+)\)") { @($matches[1], $matches[2]) }
                $nextPos = if ($nextOutput -match "Bot Position: \((\d+), (\d+)\)") { @($matches[1], $matches[2]) }
                
                # Extract scores
                $currentScore = if ($currentOutput -match "Score: (\d+)") { [int]$matches[1] } else { 0 }
                $nextScore = if ($nextOutput -match "Score: (\d+)") { [int]$matches[1] } else { 0 }
                
                if ($currentPos -and $nextPos) {
                    $moved = ($currentPos[0] -ne $nextPos[0]) -or ($currentPos[1] -ne $nextPos[1])
                    $scoreIncreased = $nextScore -gt $currentScore
                    
                    if ($moved -and -not $scoreIncreased) {
                        $missedPelletTicks += @{
                            Tick = $tick
                            Position = "($($currentPos[0]), $($currentPos[1]))"
                            NextPosition = "($($nextPos[0]), $($nextPos[1]))"
                            Score = $currentScore
                            NextScore = $nextScore
                            AdjacentPellets = ($output | Select-String "Pellet.*Yes").Matches.Count
                        }
                        
                        Write-Host "MISSED PELLET - Tick $tick" -ForegroundColor Red
                        Write-Host "  Position: ($($currentPos[0]), $($currentPos[1])) -> ($($nextPos[0]), $($nextPos[1]))" -ForegroundColor Red
                        Write-Host "  Score: $currentScore -> $nextScore (no increase)" -ForegroundColor Red
                        Write-Host "  Adjacent pellets available but not taken" -ForegroundColor Red
                        Write-Host ""
                    }
                }
            }
        }
    }
}

Write-Host "=== SUMMARY ===" -ForegroundColor Green
Write-Host "Total ticks analyzed: $totalTicks" -ForegroundColor Yellow
Write-Host "Ticks with adjacent pellets: $ticksWithAdjacentPellets" -ForegroundColor Yellow
Write-Host "Ticks where pellets were missed: $($missedPelletTicks.Count)" -ForegroundColor Red

if ($missedPelletTicks.Count -gt 0) {
    Write-Host ""
    Write-Host "=== DETAILED MISSED PELLET ANALYSIS ===" -ForegroundColor Red
    foreach ($missed in $missedPelletTicks) {
        Write-Host "Tick $($missed.Tick): $($missed.Position) -> $($missed.NextPosition), Score: $($missed.Score) -> $($missed.NextScore)" -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host "=== RECOMMENDATIONS ===" -ForegroundColor Yellow
    Write-Host "1. Check ImmediatePelletBonus heuristic weight - should be high enough to override other penalties" -ForegroundColor Yellow
    Write-Host "2. Review WallCollisionRisk and other penalty heuristics that might prevent pellet collection" -ForegroundColor Yellow
    Write-Host "3. Analyze heuristic scoring for these specific game states" -ForegroundColor Yellow
}
