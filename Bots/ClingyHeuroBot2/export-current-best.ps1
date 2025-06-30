# PowerShell script to export best individuals from ClingyHeuroBot2 evolution data
Write-Host "Exporting best individuals from ClingyHeuroBot2..." -ForegroundColor Green

try {
    # Check if evolution data exists
    if (-not (Test-Path "evolution-data")) {
        Write-Host "ERROR: No evolution-data directory found!" -ForegroundColor Red
        exit 1
    }

    # Find the latest generation
    $latestGen = Get-ChildItem "evolution-data/backups" | Sort-Object Name | Select-Object -Last 1
    if (-not $latestGen) {
        Write-Host "ERROR: No generation backups found!" -ForegroundColor Red
        exit 1
    }

    Write-Host "Latest generation: $($latestGen.Name)" -ForegroundColor Yellow

    # Get all individual files from latest generation
    $individuals = Get-ChildItem "$($latestGen.FullName)/*.json" | ForEach-Object {
        $content = Get-Content $_.FullName | ConvertFrom-Json
        $fitness = if ($content.Fitness) { [double]$content.Fitness } else { 0.0 }
        $generation = if ($content.Generation) { [int]$content.Generation } else { 0 }
        $gamesPlayed = if ($content.PerformanceHistory) { [int]$content.PerformanceHistory.Count } else { 0 }
        
        [PSCustomObject]@{
            Id = $content.Id
            Fitness = $fitness
            Generation = $generation
            GamesPlayed = $gamesPlayed
            FilePath = $_.FullName
            Content = $content
        }
    }

    # Sort by fitness (descending) and take top 5
    $bestIndividuals = $individuals | Sort-Object Fitness -Descending | Select-Object -First 5

    Write-Host "Found $($individuals.Count) individuals, exporting top $($bestIndividuals.Count):" -ForegroundColor Yellow
    foreach ($ind in $bestIndividuals) {
        Write-Host "  - Gen:$($ind.Generation) Fitness:$($ind.Fitness.ToString('F2')) Games:$($ind.GamesPlayed) ID:$($ind.Id.ToString().Substring(0,8))" -ForegroundColor Cyan
    }

    # Create export data structure
    $exportData = @{
        ExportedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
        Generation = $bestIndividuals[0].Generation
        TotalGenerations = $bestIndividuals[0].Generation
        PopulationSize = $individuals.Count
        BestFitness = $bestIndividuals[0].Fitness
        Individuals = @()
    }

    foreach ($ind in $bestIndividuals) {
        $age = if ($ind.Content.Age) { $ind.Content.Age } else { 0 }
        $evolutionMethod = if ($ind.Content.EvolutionMethod) { $ind.Content.EvolutionMethod } else { "Unknown" }
        
        $individual = @{
            Id = $ind.Content.Id
            Generation = $ind.Content.Generation
            Fitness = $ind.Content.Fitness
            GamesPlayed = $ind.GamesPlayed
            Age = $age
            EvolutionMethod = $evolutionMethod
            PerformanceSummary = "Gen:$($ind.Generation) Fitness:$($ind.Fitness.ToString('F2')) Games:$($ind.GamesPlayed)"
            Genome = $ind.Content.Genome
            BestPerformance = $null
        }
        
        # Get best performance if available
        if ($ind.Content.PerformanceHistory -and $ind.Content.PerformanceHistory.Count -gt 0) {
            $individual.BestPerformance = $ind.Content.PerformanceHistory | Sort-Object { if ($_.Fitness) { [double]$_.Fitness } else { 0.0 } } -Descending | Select-Object -First 1
        }
        
        $exportData.Individuals += $individual
    }

    # Export to JSON file
    $exportJson = $exportData | ConvertTo-Json -Depth 10
    $exportPath = "best-individuals.json"
    $exportJson | Set-Content $exportPath -Encoding UTF8

    # Create timestamped backup
    $timestamp = (Get-Date).ToString("yyyyMMdd-HHmmss")
    $backupPath = "best-individuals-gen$($exportData.Generation.ToString().PadLeft(4,'0'))-$timestamp.json"
    $exportJson | Set-Content $backupPath -Encoding UTF8

    Write-Host "✅ Export complete!" -ForegroundColor Green
    Write-Host "Files created:" -ForegroundColor Green
    Write-Host "  - $exportPath (for git)" -ForegroundColor Cyan
    Write-Host "  - $backupPath (timestamped backup)" -ForegroundColor Cyan
    
    $fileInfo = Get-Item $exportPath
    Write-Host "Export file size: $([math]::Round($fileInfo.Length / 1KB, 1)) KB" -ForegroundColor Yellow

} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} 