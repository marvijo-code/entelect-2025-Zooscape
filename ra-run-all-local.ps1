<#!
.SYNOPSIS
    Builds the Zooscape engine and all selected bots, then executes them in separate Windows Terminal tabs.
    Allows interactive stopping (keeping tabs open) and restarting of applications.
.DESCRIPTION
    1. Builds the engine (Zooscape project) in **Release** configuration.
    2. Starts the engine via **dotnet run** on port 5000 in a new Windows Terminal tab with a specific color.
    3. Sequentially builds and starts the configured bots, each in its own Windows Terminal tab with a unique color.
    4. All relevant environment variables are injected **per-process**.
    5. Interactive controls in the main script window:
        - 'q': Stops all running engine and bot processes. Terminal tabs remain open at the PowerShell prompt.
        - 'Enter' (after stopping): Restarts all applications.
        - 'c': Closes this control script, leaving applications running in their tabs.
        - 'x' (after stopping or on build error): Exits the script.
.NOTES
    ‑ Make sure that port **5000** is free before running the script.
    ‑ This script requires Windows Terminal (`wt.exe`) to be installed and accessible in PATH.
    ‑ This script purposefully avoids Docker for local debugging of network traffic and registration.
#>

function Test-PortAvailable {
    param(
        [int]$Port = 5000
    )
    
    try {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Any, $Port)
        $listener.Start()
        $listener.Stop()
        return $true
    }
    catch {
        return $false
    }
}

# Removed Test-WindowsTerminal function to avoid popups

function Stop-DotnetProcesses {
    Write-Host "Stopping processes managed by ra-run-all-local.ps1..." -ForegroundColor Yellow

    # These global script variables $engineCsproj and $bots are expected to be defined and accessible here.
    $managedProjectFiles = @((Split-Path $engineCsproj -Leaf))
    foreach ($botDefinition in $bots) {
        if ($botDefinition.Language -eq "csharp") {
            $managedProjectFiles += (Split-Path $botDefinition.Path -Leaf)
        }
    }
    $managedExecutableNames = @("Zooscape", "AdvancedMCTSBot", "DeepMCTS", "ClingyHeuroBot", "mctso4", "ClingyHeuroBotExp") # Add other bot/engine executable names as needed

    Write-Host "Identifying dotnet processes for managed projects: $($managedProjectFiles -join ', ')" -ForegroundColor DarkGray

    Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | ForEach-Object {
        $processToStop = $_
        try {
            $procInfo = Get-CimInstance Win32_Process -Filter "ProcessId = $($processToStop.Id)" -ErrorAction SilentlyContinue
            $commandLine = $procInfo.CommandLine

            if ($commandLine) {
                foreach ($projectFile in $managedProjectFiles) {
                    if ($commandLine -match [regex]::Escape($projectFile)) {
                        Write-Host "Stopping managed dotnet process: $($processToStop.ProcessName) (ID: $($processToStop.Id)) for project $projectFile" -ForegroundColor Gray
                        # For debugging: Write-Host "Full command line: $commandLine" -ForegroundColor DarkGray
                        $processToStop | Stop-Process -Force -ErrorAction Stop
                        break # Process identified and stopped, move to the next process from Get-Process
                    }
                }
            }
        }
        catch {
            Write-Warning "Error processing or stopping dotnet process $($processToStop.ProcessName) (ID: $($processToStop.Id)): $($_.Exception.Message)"
        }
    }

    # Stop C++ bots by name (if they are running as standalone executables listed in $managedExecutableNames)
    if ($managedExecutableNames.Count -gt 0) {
        Get-Process -Name $managedExecutableNames -ErrorAction SilentlyContinue | ForEach-Object {
            try {
                Write-Host "Stopping managed executable: $($_.ProcessName) (ID: $($_.Id))" -ForegroundColor Gray
                $_ | Stop-Process -Force -ErrorAction Stop
            }
            catch {
                Write-Warning "Failed to stop process $($_.ProcessName) (ID: $($_.Id)): $($_.Exception.Message)"
            }
        }
    }
    
    # The existing port 5000 check for the engine. The comment about port 5008 is misleading as it's not implemented here.
    # This part specifically targets port 5000, which is used by the engine this script manages.
    Write-Host "Checking for processes using port 5000 (engine port)..." -ForegroundColor Yellow
    try {
        netstat -ano | Select-String ":5000\s" | ForEach-Object {
            $line = $_.Line.Trim()
            $parts = $line -split '\s+'
            if ($parts.Length -ge 5) {
                $processId = $parts[-1]
                if ($processId -match '^\d+$') {
                    try {
                        $processOnPort = Get-Process -Id $processId -ErrorAction SilentlyContinue
                        if ($processOnPort) {
                            Write-Host "Found process $($processOnPort.ProcessName) (ID: $($processOnPort.Id)) using port 5000. Stopping it." -ForegroundColor Gray
                            $processOnPort | Stop-Process -Force -ErrorAction Stop
                        }
                    }
                    catch {
                        Write-Warning "Failed to stop process with PID ${processId} using port 5000: $($_.Exception.Message)"
                    }
                }
            }
        }
    }
    catch {
        Write-Warning "Failed to check port 5000 usage: $($_.Exception.Message)"
    }
    
    Write-Host "Process cleanup completed." -ForegroundColor Green
}

# Paths and Configuration
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$engineCsproj = Join-Path $scriptRoot "engine\Zooscape\Zooscape.csproj"
$engineDir = Split-Path -Parent $engineCsproj
# $visualizerApiDir = Join-Path $scriptRoot "visualizer-2d\api"

$bots = @(
    @{ Name = "ClingyHeuroBot2"; Path = "Bots\ClingyHeuroBot2\ClingyHeuroBot2.csproj"; Language = "csharp" },
    @{ Name = "AdvancedMCTSBot"; Path = "Bots\AdvancedMCTSBot"; Language = "cpp" },
    # @{ Name = "DeepMCTS"; Path = "Bots\DeepMCTS\DeepMCTS.csproj"; Language = "csharp" },
    # @{ Name = "MCTSo4"; Path = "Bots\MCTSo4\MCTSo4.csproj"; Language = "csharp" },
    # @{ Name = "ReferenceBot"; Path = "Bots\ReferenceBot\ReferenceBot.csproj"; Language = "csharp" },
    @{ Name = "ClingyHeuroBotExp"; Path = "Bots\ClingyHeuroBotExp\ClingyHeuroBotExp.csproj"; Language = "csharp" },
    @{ Name = "ClingyHeuroBot"; Path = "Bots\ClingyHeuroBot\ClingyHeuroBot.csproj"; Language = "csharp" }
)

# Tab Colors
$engineTabColor = "#0078D4" # Blue
$botTabColors = @("#107C10", "#C50F1F", "#5C2D91", "#CA5100", "#008272", "#7A7574") # Green, Red, Purple, Orange, Teal, Gray

# Initial cleanup of any lingering processes from previous runs
Stop-DotnetProcesses

$keepRunningScript = $true
$isFirstRun = $true
while ($keepRunningScript) {
    if ($isFirstRun) {
        Write-Host "========== Starting Applications ==========" -ForegroundColor Cyan
    }
    else {
        Write-Host "========== Restarting Applications ==========" -ForegroundColor Cyan
    }

    # 1. Skip engine build (assumes engine binaries are already built)
    Write-Host "[ENGINE] Skipping build for Zooscape." -ForegroundColor Yellow


    # 2. Build all bots before launching anything
    $builtBots = @()
    $buildFailed = $false
    foreach ($bot in $bots) {
        Write-Host "[BOT] Building $($bot.Name)..." -ForegroundColor Green
        $env:HUSKY = "0"
        $botProjectPath = Join-Path $scriptRoot $bot.Path
        
        if ($bot.Language -eq "cpp") {
            $botDir = Join-Path $scriptRoot $bot.Path
            Push-Location $botDir
            & .\build.bat
            if ($LASTEXITCODE -ne 0) { Pop-Location; Write-Error "Build failed for $($bot.Name)."; $buildFailed = $true; break }
            Pop-Location
        }
        else {
            dotnet build $botProjectPath -c Release
            if ($LASTEXITCODE -ne 0) { Write-Error "Build failed for $($bot.Name)."; $buildFailed = $true; break }
        }
        $builtBots += $bot
    }

    if ($buildFailed) {
        Write-Error "A bot build failed. Press Enter to retry all, or 'x' to exit script."
        while ($true) {
            $inputKey = [Console]::ReadKey($true)
            if ($inputKey.Key -eq 'Enter') { break }
            if ($inputKey.KeyChar -eq 'x') { $keepRunningScript = $false; break }
        }
        if (-not $keepRunningScript) { break } else { continue }
    }

    # 3. Stop any existing processes before launching/restarting
    if (-not $isFirstRun) {
        Write-Host "[RESTART] Stopping existing processes before restart..." -ForegroundColor Yellow
        Stop-DotnetProcesses
        Start-Sleep -Seconds 2  # Give processes time to fully terminate
    }

    # 4. Check port availability before launching engine 
    Write-Host "Checking if port 5000 is available..." -ForegroundColor DarkGray
    $port5000Available = Test-PortAvailable -Port 5000
    
    if (-not $port5000Available) {
        $busyPorts = @()
        if (-not $port5000Available) { $busyPorts += "5000" }
        Write-Warning "Port(s) $($busyPorts -join ', ') still in use. Attempting additional cleanup..."
        Stop-DotnetProcesses
        Start-Sleep -Seconds 3
        
        $port5000Available = Test-PortAvailable -Port 5000
        if (-not $port5000Available) {
            $stillBusyPorts = @()
            if (-not $port5000Available) { $stillBusyPorts += "5000" }
            Write-Error "Port(s) $($stillBusyPorts -join ', ') still not available after cleanup. Please manually stop any processes using these ports and try again."
            Write-Host "You can check what's using these ports with: netstat -ano | findstr :500" -ForegroundColor Yellow
            continue # Skip this iteration and try again
        }
    }
    Write-Host "Port 5000 is available." -ForegroundColor Green

    # 5. Launch engine (always in new tab)
    if ($isFirstRun) {
        Write-Host "[ENGINE] Launching Zooscape in new tab..." -ForegroundColor Yellow
    }
    else {
        Write-Host "[ENGINE] Launching Zooscape in new tab..." -ForegroundColor Yellow
    }
    
    $engineCommand = "dotnet run --project `"$engineCsproj`" --configuration Release"
    $encodedEngineCommand = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($engineCommand))
    
    # Use a specific window name to avoid focus stealing
    $windowName = "zooscape-runner"
    $wtArgsEngine = @("-w", $windowName, "new-tab", "--title", "Engine", "--tabColor", $engineTabColor, "--suppressApplicationTitle", "-d", $engineDir, "--", "pwsh", "-NoExit", "-NoLogo", "-EncodedCommand", $encodedEngineCommand)
    
    # Start the process in the background to avoid focus stealing
    Start-Process wt -ArgumentList $wtArgsEngine -WindowStyle Hidden

    # Give the terminal window a moment to be created before adding more tabs
    Start-Sleep -Seconds 2

    # Give the engine a moment to start listening and let the terminal settle
    Write-Host "Waiting for engine to initialize (3 more seconds)..." -ForegroundColor DarkGray
    Start-Sleep -Seconds 3

    # 6. Launch all bots in a single command to avoid focus stealing
    $botCommands = @()
    $botColorIndex = 0
    foreach ($bot in $builtBots) {
        $tokenGuid = [guid]::NewGuid().ToString()
        $envVarSetup = (
            "`$env:BOT_NICKNAME = '$($bot.Name)';",
            "`$env:Token = '$tokenGuid';",
            "`$env:BOT_TOKEN = '$tokenGuid'"
        ) -join "; "

        $botProjectPath = Join-Path $scriptRoot $bot.Path
        $botRunCommand = ""
        $botWorkingDirectory = ""

        if ($bot.Language -eq "cpp") {
            $botDir = Join-Path $scriptRoot $bot.Path
            $exePath = Join-Path $botDir "build\Release\AdvancedMCTSBot.exe"
            $botWorkingDirectory = (Split-Path $exePath -Parent)
            $botRunCommand = "& `"$exePath`""
        }
        else {
            $botWorkingDirectory = (Split-Path $botProjectPath -Parent)
            $botRunCommand = "dotnet run --project `"$botProjectPath`" --configuration Release"
        }

        $fullCommandString = "$envVarSetup; $botRunCommand"
        $encodedBotCommand = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($fullCommandString))
        $tabTitle = $bot.Name
        $currentBotTabColor = $botTabColors[$botColorIndex % $botTabColors.Count]

        # Note: The semicolon is crucial for chaining commands in wt.exe
        $botCommands += @(";", "new-tab", "--title", $tabTitle, "--tabColor", $currentBotTabColor, "--suppressApplicationTitle", "-d", $botWorkingDirectory, "--", "pwsh", "-NoExit", "-NoLogo", "-EncodedCommand", $encodedBotCommand)
        $botColorIndex++
    }

    if ($botCommands.Count -gt 0) {
        Write-Host "[BOTS] Launching all bots in new tabs..." -ForegroundColor Green
        # The first command doesn't need the leading semicolon, so we skip it.
        # Use the same window name to add tabs to the existing terminal window
        $allBotWtArgs = @("-w", $windowName) + $botCommands[1..($botCommands.Count - 1)]
        
        # Start the process in the background to avoid focus stealing
        Start-Process wt -ArgumentList $allBotWtArgs -WindowStyle Hidden
    }

    # Mark that we've completed the first run
    $isFirstRun = $false
    $Host.UI.RawUI.WindowTitle = "Zooscape Bot Manager (Running)"
        
    Write-Host "All components launched. Monitor their respective tabs for logs." -ForegroundColor Cyan
    Write-Host "Games will automatically restart every 3 minutes." -ForegroundColor Cyan
    Write-Host "Press 'q' in THIS window to STOP all application processes (tabs will remain open)." -ForegroundColor White
    Write-Host "Press 'c' in THIS window to CLOSE this script and LEAVE applications running." -ForegroundColor White

    $userAction = ''
    $gameStartTime = Get-Date
    $gameDurationMinutes = 3
    
    while ($true) {
        # Check for user input
        if ([Console]::KeyAvailable) {
            $keyInfo = [Console]::ReadKey($true)
            if ($keyInfo.KeyChar -eq 'q') { $userAction = 'stop'; break }
            if ($keyInfo.KeyChar -eq 'c') { $userAction = 'close_script'; $keepRunningScript = $false; break }
        }
        
        # Check if 3 minutes have passed
        $elapsedMinutes = ((Get-Date) - $gameStartTime).TotalMinutes
        if ($elapsedMinutes -ge $gameDurationMinutes) {
            Write-Host "`n$gameDurationMinutes minutes have elapsed. Restarting games..." -ForegroundColor Yellow
            $userAction = 'restart'
            break
        }
        
        # Show time remaining in the console title
        $timeRemaining = [math]::Ceiling($gameDurationMinutes - $elapsedMinutes)
        $Host.UI.RawUI.WindowTitle = "Next restart in: $timeRemaining minute(s) - Press 'q' to stop or 'c' to close"
        
        Start-Sleep -Milliseconds 500
    }

    if ($userAction -eq 'stop') {
        Stop-DotnetProcesses
        $Host.UI.RawUI.WindowTitle = "Zooscape Bot Manager (Stopped)"
        Write-Host "Applications stopped. Terminal tabs remain open." -ForegroundColor Yellow
        Write-Host "Press Enter to RESTART applications, or 'x' to EXIT script." -ForegroundColor White
        while ($true) {
            $keyInfo = [Console]::ReadKey($true)
            if ($keyInfo.Key -eq 'Enter') { 
                $Host.UI.RawUI.WindowTitle = "Zooscape Bot Manager (Running)"
                break 
            } # Restart by continuing outer $keepRunningScript loop
            if ($keyInfo.KeyChar -eq 'x') { $keepRunningScript = $false; break } # Exit script
        }
    }
    # If $userAction was 'close_script', $keepRunningScript is already false, loop will terminate.
}

Write-Host "Script finished." -ForegroundColor Cyan
