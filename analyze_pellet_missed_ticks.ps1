#!/usr/bin/env pwsh

# Enhanced script to analyze StaticHeuro bot's pellet collection behavior
# Identifies ticks where the bot doesn't take a pellet when one is available

param(
    [string]$LogDirectory = "logs\20250717_155412",
    [string]$BotName = "StaticHeuro",
    [int]$StartTick = 1,
    [int]$EndTick = 50
)

Write-Host "=== ENHANCED PELLET COLLECTION ANALYSIS ===" -ForegroundColor Green
Write-Host "Bot: $BotName" -ForegroundColor Yellow
Write-Host "Log Directory: $LogDirectory" -ForegroundColor Yellow
Write-Host "Analyzing ticks $StartTick to $EndTick" -ForegroundColor Yellow
Write-Host ""

$missedOpportunities = @()
$totalAnalyzed = 0

for ($tick = $StartTick; $tick -le $EndTick; $tick++) {
    $jsonFile = "$LogDirectory\$tick.json"
    
    if (Test-Path $jsonFile) {
        $totalAnalyzed++
        Write-Host "Analyzing tick $tick..." -ForegroundColor Cyan
        
        # Get current game state analysis
        $currentAnalysis = & dotnet run --project tools\GameStateInspector -- $jsonFile $BotName 2>$null
        
        if ($currentAnalysis) {
            # Parse current state
            $currentPos = if ($currentAnalysis -match "Bot Position: \((\d+), (\d+)\)") { 
                @([int]$matches[1], [int]$matches[2]) 
            } else { $null }
            
            $currentScore = if ($currentAnalysis -match "Score: (\d+)") { 
                [int]$matches[1] 
            } else { 0 }
            
            # Check for adjacent pellets
            $hasAdjacentPellet = $currentAnalysis -match "Pellet Up\? Yes|Pellet Left\? Yes|Pellet Right\? Yes|Pellet Down\? Yes"
            
            if ($hasAdjacentPellet -and $currentPos) {
                Write-Host "  Adjacent pellet found at position ($($currentPos[0]), $($currentPos[1]))" -ForegroundColor Yellow
                
                # Check next tick to see if pellet was collected
                $nextTick = $tick + 1
                $nextJsonFile = "$LogDirectory\$nextTick.json"
                
                if (Test-Path $nextJsonFile) {
                    $nextAnalysis = & dotnet run --project tools\GameStateInspector -- $nextJsonFile $BotName 2>$null
                    
                    if ($nextAnalysis) {
                        $nextPos = if ($nextAnalysis -match "Bot Position: \((\d+), (\d+)\)") { 
                            @([int]$matches[1], [int]$matches[2]) 
                        } else { $null }
                        
                        $nextScore = if ($nextAnalysis -match "Score: (\d+)") { 
                            [int]$matches[1] 
                        } else { 0 }
                        
                        if ($nextPos) {
                            $moved = ($currentPos[0] -ne $nextPos[0]) -or ($currentPos[1] -ne $nextPos[1])
                            $scoreIncreased = $nextScore -gt $currentScore
                            
                            Write-Host "    Current: ($($currentPos[0]), $($currentPos[1])), Score: $currentScore" -ForegroundColor White
                            Write-Host "    Next: ($($nextPos[0]), $($nextPos[1])), Score: $nextScore" -ForegroundColor White
                            Write-Host "    Moved: $moved, Score increased: $scoreIncreased" -ForegroundColor White
                            
                            # If bot moved but score didn't increase, it missed a pellet
                            if ($moved -and -not $scoreIncreased) {
                                Write-Host "  *** MISSED PELLET OPPORTUNITY ***" -ForegroundColor Red
                                
                                $missedOpportunities += @{
                                    Tick = $tick
                                    CurrentPos = "($($currentPos[0]), $($currentPos[1]))"
                                    NextPos = "($($nextPos[0]), $($nextPos[1]))"
                                    CurrentScore = $currentScore
                                    NextScore = $nextScore
                                    Analysis = $currentAnalysis
                                }
                            }
                            elseif (-not $moved) {
                                Write-Host "  Bot didn't move - may be stuck or choosing not to move" -ForegroundColor Orange
                            }
                            elseif ($scoreIncreased) {
                                Write-Host "  Bot successfully collected pellet" -ForegroundColor Green
                            }
                        }
                    }
                }
            }
        }
        Write-Host ""
    }
}

Write-Host "=== FINAL ANALYSIS ===" -ForegroundColor Green
Write-Host "Total ticks analyzed: $totalAnalyzed" -ForegroundColor Yellow
Write-Host "Missed pellet opportunities: $($missedOpportunities.Count)" -ForegroundColor Red

if ($missedOpportunities.Count -gt 0) {
    Write-Host ""
    Write-Host "=== MISSED OPPORTUNITIES DETAILS ===" -ForegroundColor Red
    
    foreach ($missed in $missedOpportunities) {
        Write-Host "Tick $($missed.Tick):" -ForegroundColor Red
        Write-Host "  Position: $($missed.CurrentPos) -> $($missed.NextPos)" -ForegroundColor Red
        Write-Host "  Score: $($missed.CurrentScore) -> $($missed.NextScore)" -ForegroundColor Red
        Write-Host "  Game state file: $LogDirectory\$($missed.Tick).json" -ForegroundColor Red
        Write-Host ""
    }
    
    Write-Host "=== NEXT STEPS ===" -ForegroundColor Yellow
    Write-Host "1. Use GameStateInspector to analyze the specific game states where pellets were missed" -ForegroundColor Yellow
    Write-Host "2. Check heuristic weights - ImmediatePelletBonus should be high enough to override other penalties" -ForegroundColor Yellow
    Write-Host "3. Review WallCollisionRisk and other penalty heuristics" -ForegroundColor Yellow
    Write-Host "4. Run functional tests to verify the fix" -ForegroundColor Yellow
}
else {
    Write-Host "No missed pellet opportunities found in the analyzed range." -ForegroundColor Green
    Write-Host "Consider analyzing a larger range or different game sessions." -ForegroundColor Yellow
}
