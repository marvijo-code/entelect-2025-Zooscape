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
        "DeepMCTS",
        "mctso4",
        "ClingyHeuroBot",
        "AdvancedMCTSBot"
    )
    foreach ($name in $botProcessNames) {
        Get-Process -Name $name -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    }

    Write-Host "Running engine..."
    docker compose down engine
    docker compose up -d engine

    Write-Host "Engine is running..."
    # docker compose logs -f engine

    Start-Sleep -Seconds 5

    $bots = @(
        @{ Name = "ClingyHeuroBot2"; Path = "Bots\ClingyHeuroBot2\ClingyHeuroBot2.csproj" },
        @{ Name = "AdvancedMCTSBot"; Path = "Bots\AdvancedMCTSBot"; Type = "cpp" },
        @{ Name = "DeepMCTS"; Path = "Bots\DeepMCTS\DeepMCTS.csproj" },
        # @{ Name = "MCTSo4"; Path = "Bots\MCTSo4\MCTSo4.csproj" },
        @{ Name = "ClingyHeuroBot"; Path = "Bots\ClingyHeuroBot\ClingyHeuroBot.csproj" }
    )

    $botProcs = @()
    foreach ($bot in $bots) {
        Write-Host "Building $($bot.Name)..."
        
        if ($bot.Type -eq "cpp") {
            # Handle C++ bot (AdvancedMCTSBot)
            Push-Location (Join-Path $PSScriptRoot $bot.Path)
            & ".\build.bat"
            Pop-Location
            Write-Host "Starting $($bot.Name)..."
            $workingDir = Join-Path $PSScriptRoot "$($bot.Path)\build"
            $exePath = Join-Path $workingDir "Release\AdvancedMCTSBot.exe"
            
            # Create a process with environment variables for the C++ bot
            $psi = New-Object System.Diagnostics.ProcessStartInfo
            $psi.FileName = $exePath
            $psi.WorkingDirectory = $workingDir
            $psi.UseShellExecute = $false
            $psi.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Minimized
            
            # Set environment variables
            $psi.EnvironmentVariables["RUNNER_IPV4"] = "localhost"  # Without http:// prefix
            $psi.EnvironmentVariables["RUNNER_PORT"] = "5000"
            $psi.EnvironmentVariables["BOT_NICKNAME"] = $bot.Name
            $psi.EnvironmentVariables["Token"] = [System.Guid]::NewGuid().ToString()
            $psi.EnvironmentVariables["HUB_NAME"] = "bothub"
            
            $p = [System.Diagnostics.Process]::Start($psi)
            $botProcs += $p
        }
        else {
            # Handle C# bots
            Push-Location (Join-Path $PSScriptRoot (Split-Path $bot.Path))
            dotnet build (Split-Path $bot.Path -Leaf) -c Release
            Pop-Location

            Write-Host "Starting $($bot.Name)..."
            $cmd = "/c dotnet run --project `"$PSScriptRoot\$($bot.Path)`" --configuration Release"
            $p = Start-Process "cmd.exe" -ArgumentList $cmd -WindowStyle Minimized -PassThru
            $botProcs += $p
        }
    }

    Write-Host "Waiting for engine to exit..."

    Start-Sleep -Seconds 140 -Verbose

    # Stop the engine container after the timeout
    Write-Host "Stopping engine container..."
    docker compose down engine

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
