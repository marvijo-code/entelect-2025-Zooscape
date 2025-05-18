<#
.SYNOPSIS
    Builds and runs the Zooscape engine and bots outside of Docker.
#>

Param(
    [Parameter(Mandatory = $false)]
    [int]$RunCount = 20
)

for ($i = 1; $i -le $RunCount; $i++) {
    Write-Host "Starting Run $i of $RunCount..."

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
        @{ Name = "HeuroBot"; Path = "Bots\HeuroBot\HeuroBot.csproj" },
        @{ Name = "ClingyHeuroBot"; Path = "Bots\ClingyHeuroBot\ClingyHeuroBot.csproj" }
    )

    $botProcs = @()
    foreach ($bot in $bots) {
        Write-Host "Building $($bot.Name)..."
        Push-Location (Join-Path $PSScriptRoot (Split-Path $bot.Path))
        dotnet build (Split-Path $bot.Path -Leaf) -c Release
        Pop-Location

        Write-Host "Starting $($bot.Name)..."
        $cmd = "/k dotnet run --project `"$PSScriptRoot\$($bot.Path)`" --configuration Release"
        $p = Start-Process "cmd.exe" -ArgumentList $cmd -WindowStyle Minimized -PassThru
        $botProcs += $p
    }

    Write-Host "Waiting for engine to exit..."

    Start-Sleep -Seconds 120 -Verbose

    $engineProc | Wait-Process

    Write-Host "Shutting down bots..."
    foreach ($botProcObj in $botProcs) {
        Stop-Process -Id $botProcObj.Id -ErrorAction SilentlyContinue
    }

    Write-Host "All services stopped for Run $i."
    if ($i -lt $RunCount) {
        Write-Host "Waiting for a few seconds before starting the next run..."
        Start-Sleep -Seconds 10
    }
}

Write-Host "All $RunCount runs completed."
