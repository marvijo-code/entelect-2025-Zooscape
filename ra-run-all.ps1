<#
.SYNOPSIS
    Builds and runs the Zooscape engine and bots outside of Docker.
#>

Param()

# Attempt to stop any running bot processes before starting new ones
$botProcessNames = @(
    "ReferenceBot",
    "MCTSBot",
    "mctso4",
    "HeuroBot"
)
foreach ($name in $botProcessNames) {
    Get-Process -Name $name -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}

Write-Host "Running engine..."
docker compose down engine
docker compose up -d engine

# docker compose logs -f engine

Start-Sleep -Seconds 5

$bots = @(
    @{ Name = "ClingyHeuroBot2"; Path = "Bots\ClingyHeuroBot2\ClingyHeuroBot2.csproj" },
    @{ Name = "ClingyHeuroBotExp"; Path = "Bots\ClingyHeuroBotExp\ClingyHeuroBotExp.csproj" },
    @{ Name = "ClingyHeuroBot"; Path = "Bots\ClingyHeuroBot\ClingyHeuroBot.csproj" },
    @{ Name = "HeuroBot"; Path = "Bots\HeuroBot\HeuroBot.csproj" }
)

$botProcs = @()
foreach ($bot in $bots) {
    Write-Host "Building $($bot.Name)..."
    Push-Location (Join-Path $PSScriptRoot (Split-Path $bot.Path))
    dotnet build (Split-Path $bot.Path -Leaf) -c Release
    Pop-Location

    Write-Host "Starting $($bot.Name)..."
    $cmd = "/k dotnet run --project `"$PSScriptRoot\$($bot.Path)`" --configuration Release"
    $p = Start-Process "cmd.exe" -ArgumentList $cmd -PassThru
    $botProcs += $p
}

# wait 3 minutes
Write-Host "Waiting for engine to exit..."

Start-Sleep -Seconds 360 -Verbose

$engineProc | Wait-Process

Write-Host "Shutting down bots..."
foreach ($p in $botProcs) {
    Stop-Process -Id $p.Id -ErrorAction SilentlyContinue
}

Write-Host "All services stopped."
