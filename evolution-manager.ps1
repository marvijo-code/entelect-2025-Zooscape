#!
<#
.SYNOPSIS
    Manages the genetic algorithm evolution system for ClingyHeuroBot2
.DESCRIPTION
    Provides commands to:
    - View evolution statistics
    - Trigger evolution to next generation
    - View high scores
    - Export evolution data
.NOTES
    Run this alongside ra-run-all-local.ps1 to manage evolution
#>

param(
    [Parameter(Position=0)]
    [ValidateSet("stats", "evolve", "scores", "export", "help")]
    [string]$Command = "help"
)

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$highScoresFile = Join-Path $scriptRoot "high-scores.json"
$evolutionDataDir = Join-Path $scriptRoot "Bots\ClingyHeuroBot2\evolution-data"

function Show-Help {
    Write-Host "Evolution Manager Commands:" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  stats    - Show current evolution statistics" -ForegroundColor Green
    Write-Host "  evolve   - Trigger evolution to next generation" -ForegroundColor Green
    Write-Host "  scores   - Show high scores from evolution" -ForegroundColor Green
    Write-Host "  export   - Export evolution data to CSV" -ForegroundColor Green
    Write-Host "  help     - Show this help message" -ForegroundColor Green
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Yellow
    Write-Host "  .\evolution-manager.ps1 stats" -ForegroundColor Gray
    Write-Host "  .\evolution-manager.ps1 evolve" -ForegroundColor Gray
    Write-Host "  .\evolution-manager.ps1 scores" -ForegroundColor Gray
}

function Show-EvolutionStats {
    Write-Host "=== EVOLUTION STATISTICS ===" -ForegroundColor Cyan
    
    try {
        # Try to read from evolution data files
        $populationFile = Join-Path $evolutionDataDir "population.json"
        $statsFile = Join-Path $evolutionDataDir "statistics.json"
        
        if (Test-Path $populationFile) {
            $population = Get-Content $populationFile | ConvertFrom-Json
            Write-Host "Population Size: $($population.Individuals.Count)" -ForegroundColor Green
            Write-Host "Current Generation: $($population.Generation)" -ForegroundColor Green
            
            if ($population.Individuals.Count -gt 0) {
                $avgFitness = ($population.Individuals | Measure-Object -Property Fitness -Average).Average
                $maxFitness = ($population.Individuals | Measure-Object -Property Fitness -Maximum).Maximum
                $minFitness = ($population.Individuals | Measure-Object -Property Fitness -Minimum).Minimum
                
                Write-Host "Average Fitness: $($avgFitness.ToString('F2'))" -ForegroundColor Green
                Write-Host "Best Fitness: $($maxFitness.ToString('F2'))" -ForegroundColor Green
                Write-Host "Worst Fitness: $($minFitness.ToString('F2'))" -ForegroundColor Green
                
                $bestIndividual = $population.Individuals | Sort-Object Fitness -Descending | Select-Object -First 1
                Write-Host "Best Individual: Gen $($bestIndividual.Generation), $($bestIndividual.GamesPlayed) games" -ForegroundColor Yellow
            }
        } else {
            Write-Host "No population data found. Evolution system may not be initialized yet." -ForegroundColor Yellow
        }
        
        if (Test-Path $statsFile) {
            $stats = Get-Content $statsFile | ConvertFrom-Json
            Write-Host "Total Generations: $($stats.Generation)" -ForegroundColor Green
        }
    }
    catch {
        Write-Host "Error reading evolution statistics: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    Write-Host "=============================" -ForegroundColor Cyan
}

function Show-HighScores {
    Write-Host "=== HIGH SCORES ===" -ForegroundColor Cyan
    
    if (Test-Path $highScoresFile) {
        try {
            $jsonContent = Get-Content $highScoresFile -Raw | ConvertFrom-Json
            
            if ($jsonContent -and $jsonContent.Count -gt 0) {
                $topScores = $jsonContent | Sort-Object Score -Descending | Select-Object -First 10
                
                Write-Host "Top 10 Performances:" -ForegroundColor Green
                for ($i = 0; $i -lt $topScores.Count; $i++) {
                    $rank = $i + 1
                    $score = $topScores[$i]
                    $timestamp = if ($score.Timestamp) { ([DateTime]$score.Timestamp).ToString("yyyy-MM-dd HH:mm:ss") } else { "Unknown" }
                    Write-Host "$rank. Score: $($score.Score) | Fitness: $($score.Fitness) | Gen: $($score.Generation) | Time: $timestamp" -ForegroundColor White
                }
            } else {
                Write-Host "No high scores recorded yet." -ForegroundColor Yellow
            }
        }
        catch {
            Write-Host "Error reading high scores: $($_.Exception.Message)" -ForegroundColor Red
        }
    } else {
        Write-Host "High scores file not found: $highScoresFile" -ForegroundColor Yellow
        Write-Host "Run some games with evolved bots to generate scores." -ForegroundColor Gray
    }
    
    Write-Host "===================" -ForegroundColor Cyan
}

function Trigger-Evolution {
    Write-Host "=== TRIGGERING EVOLUTION ===" -ForegroundColor Cyan
    
    # This would require integration with the C# evolution system
    # For now, show instructions on how to manually trigger evolution
    Write-Host "To trigger evolution, you need to:" -ForegroundColor Yellow
    Write-Host "1. Stop the current bot instances (press 'q' in ra-run-all-local.ps1)" -ForegroundColor Gray
    Write-Host "2. The evolution system will automatically evolve after collecting performance data" -ForegroundColor Gray
    Write-Host "3. Restart the bots to use new evolved individuals" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Alternatively, you can implement a REST API in the bot to trigger evolution remotely." -ForegroundColor Green
    
    Write-Host "===============================" -ForegroundColor Cyan
}

function Export-EvolutionData {
    Write-Host "=== EXPORTING EVOLUTION DATA ===" -ForegroundColor Cyan
    
    $exportDir = Join-Path $scriptRoot "evolution-exports"
    if (!(Test-Path $exportDir)) {
        New-Item -ItemType Directory -Path $exportDir | Out-Null
    }
    
    $timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
    $exportFile = Join-Path $exportDir "evolution-data_$timestamp.csv"
    
    try {
        # Export high scores to CSV
        if (Test-Path $highScoresFile) {
            $jsonContent = Get-Content $highScoresFile -Raw | ConvertFrom-Json
            
            if ($jsonContent -and $jsonContent.Count -gt 0) {
                $csvData = $jsonContent | Select-Object @{
                    Name = 'IndividualId'
                    Expression = { $_.IndividualId }
                }, @{
                    Name = 'Score'
                    Expression = { $_.Score }
                }, @{
                    Name = 'Fitness' 
                    Expression = { $_.Fitness }
                }, @{
                    Name = 'Generation'
                    Expression = { $_.Generation }
                }, @{
                    Name = 'Rank'
                    Expression = { $_.Rank }
                }, @{
                    Name = 'TotalPlayers'
                    Expression = { $_.TotalPlayers }
                }, @{
                    Name = 'Timestamp'
                    Expression = { $_.Timestamp }
                }
                
                $csvData | Export-Csv -Path $exportFile -NoTypeInformation
                Write-Host "Evolution data exported to: $exportFile" -ForegroundColor Green
                Write-Host "Records exported: $($csvData.Count)" -ForegroundColor Green
            } else {
                Write-Host "No valid evolution data found to export." -ForegroundColor Yellow
            }
        } else {
            Write-Host "No high scores file found to export." -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "Error exporting evolution data: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    Write-Host "=================================" -ForegroundColor Cyan
}

# Main execution
switch ($Command.ToLower()) {
    "stats" { Show-EvolutionStats }
    "evolve" { Trigger-Evolution }
    "scores" { Show-HighScores }
    "export" { Export-EvolutionData }
    "help" { Show-Help }
    default { Show-Help }
} 