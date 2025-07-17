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

    # Define the ports for services that this script manages.
    # Port 5008 is explicitly excluded as requested.
    $managedPorts = @(5000, 5001, 5002, 5003, 5004, 5005, 5006, 5007, 5009, 5010, 5011, 5012, 5013, 5014, 5015)
    $stoppedProcesses = @()

    Write-Host "Stopping processes by port: $($managedPorts -join ', ')" -ForegroundColor DarkGray

    foreach ($port in $managedPorts) {
        try {
            # Find processes listening on the specified port.
            $netstatResult = netstat -ano | Select-String -Pattern ":$($port)\s" | Where-Object { $_ -match 'LISTENING' }
            
            foreach ($line in $netstatResult) {
                $parts = $line.Line.Trim() -split '\s+'
                if ($parts.Length -ge 4) {
                    $processId = $parts[-1]
                    if ($processId -match '^\d+$' -and [int]$processId -ne 0) {
                        try {
                            $processToStop = Get-Process -Id $processId -ErrorAction SilentlyContinue
                            if ($processToStop) {
                                Write-Host "Stopping process $($processToStop.ProcessName) (ID: $processId) on port $port" -ForegroundColor Gray
                                $processToStop | Stop-Process -Force -ErrorAction Stop
                                if (-not ($stoppedProcesses -contains [int]$processId)) {
                                    $stoppedProcesses += [int]$processId
                                }
                            }
                        }
                        catch {
                            Write-Warning "Failed to stop process with PID $processId on port ${port}: $($_.Exception.Message)"
                        }
                    }
                }
            }
        }
        catch {
            Write-Warning "Failed to check or stop processes on port ${port}: $($_.Exception.Message)"
        }
    }

    Write-Host "[DIAGNOSTIC] Process cleanup finished." -ForegroundColor Magenta
    
    # Wait for processes to fully terminate and release file locks
    if ($stoppedProcesses.Count -gt 0) {
        Write-Host "Waiting for processes to fully terminate and release file locks..." -ForegroundColor Yellow
        $maxWaitSeconds = 10
        $waitStart = Get-Date
        
        do {
            $stillRunning = @()
            foreach ($processId in $stoppedProcesses) {
                try {
                    $proc = Get-Process -Id $processId -ErrorAction SilentlyContinue
                    if ($proc) {
                        $stillRunning += $processId
                    }
                }
                catch {
                    # Process no longer exists, which is what we want
                }
            }
            
            if ($stillRunning.Count -eq 0) {
                break
            }
            
            $elapsedSeconds = ((Get-Date) - $waitStart).TotalSeconds
            if ($elapsedSeconds -ge $maxWaitSeconds) {
                Write-Warning "Some processes are still running after $maxWaitSeconds seconds: $($stillRunning -join ', ')"
                break
            }
            
            Start-Sleep -Milliseconds 500
        } while ($true)
        
        # Additional wait for file system to release locks
        Write-Host "Waiting additional time for file system locks to be released..." -ForegroundColor DarkGray
        Start-Sleep -Seconds 3
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
    # @{ Name = "ClingyHeuroBotExp"; Path = "Bots\ClingyHeuroBotExp\ClingyHeuroBotExp.csproj"; Language = "csharp" },
    # @{ Name = "ReferenceBot"; Path = "engine\ReferenceBot\ReferenceBot.csproj"; Language = "csharp" },
    # @{ Name = "RLPlayBot"; Path = "Bots\rl"; Language = "python" },
    @{ Name = "ClingyHeuroBot"; Path = "Bots\ClingyHeuroBot\ClingyHeuroBot.csproj"; Language = "csharp" },
    @{ Name = "AdvancedMCTSBot"; Path = "Bots\AdvancedMCTSBot"; Language = "cpp" },
    @{ Name = "StaticHeuro"; Path = "Bots\StaticHeuro\StaticHeuro.csproj"; Language = "csharp" }
    # @{ Name = "DeepMCTS"; Path = "Bots\DeepMCTS\DeepMCTS.csproj"; Language = "csharp" },
    # @{ Name = "MCTSo4"; Path = "Bots\MCTSo4\MCTSo4.csproj"; Language = "csharp" },
)

# Tab Colors
$engineTabColor = "#0078D4" # Blue
$botTabColors = @("#107C10", "#C50F1F", "#5C2D91", "#CA5100", "#008272", "#7A7574") # Green, Red, Purple, Orange, Teal, Gray

# Initial cleanup of any lingering processes from previous runs
Write-Host "Performing initial cleanup of any lingering processes..." -ForegroundColor Cyan
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


    # 2. Stop any existing processes before building/restarting
    if (-not $isFirstRun) {
        Write-Host "[RESTART] Stopping existing processes before restart..." -ForegroundColor Yellow
        Stop-DotnetProcesses
        Write-Host "Waiting additional time before building to ensure file locks are released..." -ForegroundColor DarkGray
        Start-Sleep -Seconds 2  # Additional wait for file system
    }

    # 3. Build all bots before launching anything
    # Double-check that no processes are still running that could cause file locks by re-running the cleanup.
    Write-Host "Verifying no conflicting processes are running before build..." -ForegroundColor DarkGray
    Stop-DotnetProcesses
    Start-Sleep -Seconds 1 # Brief pause to ensure processes have terminated.
    
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
        elseif ($bot.Language -eq "python") {
            $botDir = Join-Path $scriptRoot $bot.Path
            Write-Host "Checking Python environment for $($bot.Name)..." -ForegroundColor DarkGray
            # Check if virtual environment exists and has required packages
            $venvPath = Join-Path $botDir ".venv"
            if (-not (Test-Path $venvPath)) {
                Write-Error "Python virtual environment not found at $venvPath for $($bot.Name). Please set up the virtual environment first."
                $buildFailed = $true; break
            }
            Write-Host "Python environment OK for $($bot.Name)." -ForegroundColor Green
        }
        else {
            # Retry build if it fails due to file locks
            $buildRetries = 3
            $buildSuccess = $false
            for ($retry = 1; $retry -le $buildRetries; $retry++) {
                try {
                    Write-Host "Building $($bot.Name) (attempt $retry/$buildRetries)..." -ForegroundColor DarkGray
                    dotnet build $botProjectPath -c Release --verbosity quiet
                    if ($LASTEXITCODE -eq 0) {
                        $buildSuccess = $true
                        break
                    }
                }
                catch {
                    Write-Warning "Build attempt $retry failed: $($_.Exception.Message)"
                }
                
                if ($retry -lt $buildRetries) {
                    Write-Host "Build failed, waiting before retry..." -ForegroundColor Yellow
                    Start-Sleep -Seconds 2
                }
            }
            
            if (-not $buildSuccess) {
                Write-Error "Build failed for $($bot.Name) after $buildRetries attempts."
                $buildFailed = $true
                break
            }
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

    # 5. Prepare engine command
    $engineCommand = "dotnet run --project `"$engineCsproj`" --configuration Release"
    $encodedEngineCommand = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($engineCommand))
    
    # Use a specific window name to avoid focus stealing
    $windowName = "zooscape-runner"
    
    # Build engine tab arguments
    $engineTabArgs = @("new-tab", "--title", "Engine", "--tabColor", $engineTabColor, "--suppressApplicationTitle", "-d", $engineDir, "--", "pwsh", "-NoExit", "-NoLogo", "-EncodedCommand", $encodedEngineCommand)

    # 6. Prepare all bot commands
    $allTabCommands = @()
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
        elseif ($bot.Language -eq "python") {
            $botDir = Join-Path $scriptRoot $bot.Path
            $botWorkingDirectory = $botDir
            $pythonExe = Join-Path $botDir ".venv\Scripts\python.exe"
            $botRunCommand = "& `"$pythonExe`" play_bot_runner.py"
        }
        else {
            $botWorkingDirectory = (Split-Path $botProjectPath -Parent)
            $botRunCommand = "dotnet run --project `"$botProjectPath`" --configuration Release"
        }

        $fullCommandString = "$envVarSetup; $botRunCommand"
        $encodedBotCommand = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($fullCommandString))
        $tabTitle = $bot.Name
        $currentBotTabColor = $botTabColors[$botColorIndex % $botTabColors.Count]

        # Add bot tab arguments
        $allTabCommands += @(";", "new-tab", "--title", $tabTitle, "--tabColor", $currentBotTabColor, "--suppressApplicationTitle", "-d", $botWorkingDirectory, "--", "pwsh", "-NoExit", "-NoLogo", "-EncodedCommand", $encodedBotCommand)
        $botColorIndex++
    }

    # 7. Launch all tabs at once in a single command
    Write-Host "[ALL] Launching engine and all bots in new tabs simultaneously..." -ForegroundColor Cyan
    
    # Combine engine and bot commands into a single wt invocation
    $allWtArgs = @("-w", $windowName) + $engineTabArgs + $allTabCommands
    
    # Start all processes in a visible window
    Start-Process wt -ArgumentList $allWtArgs -WindowStyle Normal
    
    # Give the applications a moment to start
    Write-Host "Waiting for all applications to initialize..." -ForegroundColor DarkGray
    Start-Sleep -Seconds 5

    # Mark that we've completed the first run
    $isFirstRun = $false
    $Host.UI.RawUI.WindowTitle = "Zooscape Bot Manager (Running)"
        
    Write-Host "All components launched. Monitor their respective tabs for logs." -ForegroundColor Cyan
    Write-Host "Games will automatically restart every 2 minutes." -ForegroundColor Cyan
    Write-Host "Press 'q' in THIS window to STOP all application processes (tabs will remain open)." -ForegroundColor White
    Write-Host "Press 'c' in THIS window to CLOSE this script and LEAVE applications running." -ForegroundColor White

    $userAction = ''
    $gameStartTime = Get-Date
    $gameDurationMinutes = 4
    
    while ($true) {
        # Check for user input
        if ([Console]::KeyAvailable) {
            $keyInfo = [Console]::ReadKey($true)
            if ($keyInfo.KeyChar -eq 'q') { $userAction = 'stop'; break }
            if ($keyInfo.KeyChar -eq 'c') { $userAction = 'close_script'; $keepRunningScript = $false; break }
        }
        
        # Check if game duration has passed
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
