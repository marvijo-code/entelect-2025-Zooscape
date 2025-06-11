<#!
.SYNOPSIS
    Builds the Zooscape engine and all selected bots, then executes them **without** Docker so that network traffic and registration can be inspected locally.
.DESCRIPTION
    1. Builds the engine (Zooscape project) in **Release** configuration.
    2. Starts the engine via **dotnet run** on port 5000 in a new PowerShell window so that logs are visible.
    3. Sequentially builds and starts the configured bots, each in its own window.
    4. All relevant environment variables are injected **per-process**, allowing easy tweaking.
    5. Supports multiple sequential runs through the -RunCount switch (default = 1).

.NOTES
    ‑ Make sure that port **5000** is free before running the script.
    ‑ Press **Ctrl + C** in the engine window (or close it) to stop the current run early.
    ‑ This script purposefully avoids Docker so we can debug AdvancedMCTSBot registration issues.
#>
param(
    # How many complete game runs to perform
    [int]$RunCount = 1
)

function Stop-DotnetProcesses {
    # Try stop any previously running dotnet instances of Zooscape or bots
    Get-Process -Name "dotnet", "AdvancedMCTSBot", "DeepMCTS", "ClingyHeuroBot", "mctso4" -ErrorAction SilentlyContinue | ForEach-Object {
        try { $_ | Stop-Process -Force -ErrorAction SilentlyContinue }
        catch {}
    }
}

# Paths
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$engineCsproj = Join-Path $scriptRoot "engine\Zooscape\Zooscape.csproj"

$bots = @(
    @{ Name = "ClingyHeuroBot2"; Path = "Bots\ClingyHeuroBot2\ClingyHeuroBot2.csproj"; Language = "csharp" },
    @{ Name = "AdvancedMCTSBot"; Path = "Bots\AdvancedMCTSBot"; Language = "cpp" },
    @{ Name = "DeepMCTS"; Path = "Bots\DeepMCTS\DeepMCTS.csproj"; Language = "csharp" },
    @{ Name = "MCTSo4"; Path = "Bots\MCTSo4\MCTSo4.csproj"; Language = "csharp" },
    @{ Name = "ClingyHeuroBot"; Path = "Bots\ClingyHeuroBot\ClingyHeuroBot.csproj"; Language = "csharp" }
)

for ($runIndex = 1; $runIndex -le $RunCount; $runIndex++) {
    Write-Host "========== Run $runIndex / $RunCount ==========" -ForegroundColor Cyan
    Stop-DotnetProcesses

    # 1. Build engine
    Write-Host "[ENGINE] Building Zooscape…" -ForegroundColor Yellow
    $env:HUSKY = "0"
    dotnet build $engineCsproj -c Release
    if ($LASTEXITCODE -ne 0) { throw "Engine build failed." }

    # 2. Start engine in new window (so we can inspect its logs)
    Write-Host "[ENGINE] Launching Zooscape…" -ForegroundColor Yellow
    $engineCommand = "dotnet run --project '$engineCsproj' --configuration Release"
    $argumentList = @("-NoExit", "-NoLogo", "-Command", $engineCommand)
    $engineWindow = Start-Process pwsh -ArgumentList $argumentList -PassThru -WindowStyle Normal

    # Give the engine a moment to start listening. This may need to be increased if negotiation still fails.
    Start-Sleep -Seconds 10

    # 3. Build and launch every bot in its own window that remains open
    $botProcesses = @()
    foreach ($bot in $bots) {
        Write-Host "[BOT] Building $($bot.Name)…" -ForegroundColor Green
        $env:HUSKY = "0" # Disable husky hooks for all builds

        # Prepare ProcessStartInfo for launching the bot
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = "pwsh.exe"
        $psi.UseShellExecute = $false
        $psi.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Normal
        $psi.EnvironmentVariables["RUNNER_IPV4"]  = "localhost"
        $psi.EnvironmentVariables["RUNNER_PORT"]  = "5000"
        $psi.EnvironmentVariables["BOT_NICKNAME"] = $bot.Name
        $psi.EnvironmentVariables["HUB_NAME"]     = "bothub"
        # C++ bot expects "Token", C# bots expect "BOT_TOKEN"
        $tokenGuid = [guid]::NewGuid().ToString()
        $psi.EnvironmentVariables["Token"]        = $tokenGuid
        $psi.EnvironmentVariables["BOT_TOKEN"]    = $tokenGuid

        if ($bot.Language -eq "cpp") {
            # AdvancedMCTSBot (C++)
            $botDir = Join-Path $scriptRoot $bot.Path
            Push-Location $botDir
            & .\build.bat
            if ($LASTEXITCODE -ne 0) { Pop-Location; throw "Build failed for $($bot.Name)." }
            Pop-Location

            $exePath = Join-Path $botDir "build\Release\AdvancedMCTSBot.exe"
            $psi.WorkingDirectory = (Split-Path $exePath -Parent)
            $psi.Arguments = "-NoExit", "-NoLogo", "-Command", "& '$exePath'"
        }
        else {
            # C# bots
            $csprojPath = Join-Path $scriptRoot $bot.Path
            dotnet build $csprojPath -c Release
            if ($LASTEXITCODE -ne 0) { throw "Build failed for $($bot.Name)." }

            $psi.WorkingDirectory = (Split-Path $csprojPath -Parent)
            $psi.Arguments = "-NoExit", "-NoLogo", "-Command", "dotnet run --project `"$csprojPath`" --configuration Release"
        }

        Write-Host "[BOT] Launching $($bot.Name)…" -ForegroundColor Green
        $botProcesses += [System.Diagnostics.Process]::Start($psi)
    }

    Write-Host "All components launched. Monitor the engine window for registration events." -ForegroundColor Cyan
    Write-Host "Press 'q' in this window to stop all processes..."
    while ($true) {
        if ($engineWindow.HasExited) {
            Write-Warning "Engine process exited prematurely."
            break
        }
        if ([console]::KeyAvailable) {
            if ([console]::ReadKey($true).Key -eq 'q') {
                break
            }
        }
        Start-Sleep -Milliseconds 200
    }

    # Attempt a graceful shutdown
    Write-Host "Stopping bots & engine…" -ForegroundColor Yellow
    foreach ($p in $botProcesses) { try { $p.CloseMainWindow() | Out-Null } catch {} }
    Stop-DotnetProcesses
    try { $engineWindow.CloseMainWindow() | Out-Null } catch {}
    Start-Sleep -Seconds 3
}
