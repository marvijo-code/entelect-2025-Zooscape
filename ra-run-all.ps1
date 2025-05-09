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
    @{ Name = "RefBot-0"; Path = "Bots\ReferenceBot\ReferenceBot.csproj" },
    @{ Name = "g2-mcts"; Path = "Bots\MCTSBot\MCTSBot.csproj" },
    @{ Name = "o4mcts"; Path = "Bots\mctso4\mctso4.csproj" },
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
Start-Sleep -Seconds 180

Write-Host "Waiting for engine to exit..."
$engineProc | Wait-Process

Write-Host "Shutting down bots..."
foreach ($p in $botProcs) {
    Stop-Process -Id $p.Id -ErrorAction SilentlyContinue
}

Write-Host "All services stopped."
