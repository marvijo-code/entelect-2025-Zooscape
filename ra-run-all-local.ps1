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

function Stop-DotnetProcesses {
    # Try stop any previously running dotnet instances of Zooscape or bots
    Write-Host "Stopping dotnet, AdvancedMCTSBot, DeepMCTS, ClingyHeuroBot, mctso4 processes..." -ForegroundColor Yellow
    Get-Process -Name "dotnet", "AdvancedMCTSBot", "DeepMCTS", "ClingyHeuroBot", "mctso4" -ErrorAction SilentlyContinue | ForEach-Object {
        try {
            Write-Host "Stopping process $($_.ProcessName) (ID: $($_.Id))" -ForegroundColor Gray
            $_ | Stop-Process -Force -ErrorAction Stop
        }
        catch {
            Write-Warning "Failed to stop process $($_.ProcessName) (ID: $($_.Id)): $($_.Exception.Message)"
        }
    }
}

# Paths and Configuration
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$engineCsproj = Join-Path $scriptRoot "engine\Zooscape\Zooscape.csproj"
$engineDir = Split-Path -Parent $engineCsproj

$bots = @(
    @{ Name = "ClingyHeuroBot2"; Path = "Bots\ClingyHeuroBot2\ClingyHeuroBot2.csproj"; Language = "csharp" },
    # @{ Name = "AdvancedMCTSBot"; Path = "Bots\AdvancedMCTSBot"; Language = "cpp" },
    @{ Name = "DeepMCTS"; Path = "Bots\DeepMCTS\DeepMCTS.csproj"; Language = "csharp" },
    @{ Name = "MCTSo4"; Path = "Bots\MCTSo4\MCTSo4.csproj"; Language = "csharp" },
    @{ Name = "ClingyHeuroBot"; Path = "Bots\ClingyHeuroBot\ClingyHeuroBot.csproj"; Language = "csharp" }
)

# Tab Colors
$engineTabColor = "#0078D4" # Blue
$botTabColors = @("#107C10", "#C50F1F", "#5C2D91", "#CA5100", "#008272", "#7A7574") # Green, Red, Purple, Orange, Teal, Gray

# Initial cleanup of any lingering processes from previous runs
Stop-DotnetProcesses

$keepRunningScript = $true
while ($keepRunningScript) {
    Write-Host "========== Starting Applications ==========" -ForegroundColor Cyan

    # 1. Build engine
    Write-Host "[ENGINE] Building Zooscape…" -ForegroundColor Yellow
    $env:HUSKY = "0"
    dotnet build $engineCsproj -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Engine build failed. Press Enter to retry, or 'x' to exit script."
        while ($true) {
            $inputKey = [Console]::ReadKey($true)
            if ($inputKey.Key -eq 'Enter') { break } # Retry build
            if ($inputKey.KeyChar -eq 'x') { $keepRunningScript = $false; break } # Exit script
        }
        if (-not $keepRunningScript) { continue } # Exit outer loop
        else { continue } # Retry build by restarting outer loop
    }

    # 2. Build all bots before launching anything
    $builtBots = @()
    $buildFailed = $false
    foreach ($bot in $bots) {
        Write-Host "[BOT] Building $($bot.Name)…" -ForegroundColor Green
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

    # 3. Construct a single Windows Terminal command
    Write-Host "Constructing Windows Terminal command..." -ForegroundColor DarkGray
    $wtCommand = "wt.exe -w 0 " # Open in the current window

    # Engine Tab
    $engineCommand = "dotnet run --project `"$engineCsproj`" --configuration Release"
    $encodedEngineCommand = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($engineCommand))
    $wtCommand += "new-tab --title Engine --tabColor $engineTabColor -d `"$engineDir`" -- pwsh -NoExit -NoLogo -EncodedCommand $encodedEngineCommand; "

    # Give the engine a moment to start listening before bots connect
    $wtCommand += "split-pane -p `"General`" -- pwsh -Command `"Write-Host 'Waiting for engine to initialize (10 seconds)...'; Start-Sleep -Seconds 10;`"; "
    
    # Bot Tabs
    $botColorIndex = 0
    foreach ($bot in $builtBots) {
        $tokenGuid = [guid]::NewGuid().ToString()
        $envVarSetup = (
            "`$env:BOT_NICKNAME = '$($bot.Name)';",
            "`$env:Token = '$tokenGuid';",
            "`$env:BOT_TOKEN = '$tokenGuid'"
        ) -join " "

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
        
        $wtCommand += "new-tab --title `"$tabTitle`" --tabColor `"$currentBotTabColor`" -d `"$botWorkingDirectory`" -- pwsh -NoExit -NoLogo -EncodedCommand $encodedBotCommand; "
        $botColorIndex++
    }

    # 4. Launch all tabs
    Write-Host "[SYSTEM] Launching all components in Windows Terminal..." -ForegroundColor Yellow
    Invoke-Expression $wtCommand
}
if (-not $keepRunningScript) { continue } # If a bot build failed and user chose to exit script

Write-Host "All components launched. Monitor their respective tabs for logs." -ForegroundColor Cyan
Write-Host "Press 'q' in THIS window to STOP all application processes (tabs will remain open)." -ForegroundColor White
Write-Host "Press 'c' in THIS window to CLOSE this script and LEAVE applications running." -ForegroundColor White

$userAction = ''
while ($true) {
    if ([Console]::KeyAvailable) {
        $keyInfo = [Console]::ReadKey($true)
        if ($keyInfo.KeyChar -eq 'q') { $userAction = 'stop'; break }
        if ($keyInfo.KeyChar -eq 'c') { $userAction = 'close_script'; $keepRunningScript = $false; break }
    }
    Start-Sleep -Milliseconds 200
}

if ($userAction -eq 'stop') {
    Stop-DotnetProcesses
    Write-Host "Applications stopped. Terminal tabs remain open." -ForegroundColor Yellow
    Write-Host "Press Enter to RESTART applications, or 'x' to EXIT script." -ForegroundColor White
    while ($true) {
        $keyInfo = [Console]::ReadKey($true)
        if ($keyInfo.Key -eq 'Enter') { break } # Restart by continuing outer $keepRunningScript loop
        if ($keyInfo.KeyChar -eq 'x') { $keepRunningScript = $false; break } # Exit script
    }
}
# If $userAction was 'close_script', $keepRunningScript is already false, loop will terminate.
}

Write-Host "Script finished." -ForegroundColor Cyan
