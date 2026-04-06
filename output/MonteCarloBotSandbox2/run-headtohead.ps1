param(
    [int[]]$Seeds = @(101, 102, 103, 104, 105),
    [string]$BotAName = "MonteCarloBot",
    [string]$BotADll = "",
    [string]$BotBName = "ClingyHeuroBot2",
    [string]$BotBDll = "",
    [int]$NumberOfBots = 2,
    [int]$MaxTicks = 600,
    [int]$StartGameTimeout = 20,
    [int]$TickDuration = 5,
    [int]$BasePort = 5000,
    [string]$RunName = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if ([string]::IsNullOrWhiteSpace($RunName)) {
    $RunName = "{0}_{1}_vs_{2}" -f (Get-Date -Format "yyyyMMdd_HHmmss"), $BotAName, $BotBName
}

$runRoot = Join-Path $PSScriptRoot ("runs\" + $RunName)
New-Item -ItemType Directory -Force -Path $runRoot | Out-Null

function Get-FreeTcpPort([int]$preferredPort) {
    try {
        $listener = [System.Net.Sockets.TcpListener]::Create($preferredPort)
        $listener.Start()
        $listener.Stop()
        return $preferredPort
    }
    catch {
        $fallback = [System.Net.Sockets.TcpListener]::Create(0)
        $fallback.Start()
        $port = ([System.Net.IPEndPoint]$fallback.LocalEndpoint).Port
        $fallback.Stop()
        return $port
    }
}

function Resolve-Dll([string]$configuredPath, [string]$botName) {
    if (-not [string]::IsNullOrWhiteSpace($configuredPath)) {
        return (Resolve-Path $configuredPath).Path
    }

    $defaultPath = Join-Path $repoRoot "Bots\$botName\bin\Debug\net8.0\$botName.dll"
    return (Resolve-Path $defaultPath).Path
}

function Start-LoggedProcess(
    [string]$filePath,
    [string]$arguments,
    [string]$workingDirectory,
    [hashtable]$environment,
    [string]$stdoutPath,
    [string]$stderrPath
) {
    $previousValues = @{}
    foreach ($key in $environment.Keys) {
        $previousValues[$key] = [Environment]::GetEnvironmentVariable($key, "Process")
        [Environment]::SetEnvironmentVariable($key, [string]$environment[$key], "Process")
    }

    try {
        return Start-Process -FilePath $filePath `
            -ArgumentList $arguments `
            -WorkingDirectory $workingDirectory `
            -RedirectStandardOutput $stdoutPath `
            -RedirectStandardError $stderrPath `
            -WindowStyle Hidden `
            -PassThru
    }
    finally {
        foreach ($key in $environment.Keys) {
            [Environment]::SetEnvironmentVariable($key, $previousValues[$key], "Process")
        }
    }
}

function Stop-LoggedProcess($process) {
    if ($null -eq $process) { return }
    try {
        if (-not $process.HasExited) {
            $process.Kill($true)
            $process.WaitForExit(5000) | Out-Null
        }
    } catch {}
    try { $process.Dispose() } catch {}
}

function Get-Placement([string[]]$lines, [string]$nickname) {
    foreach ($line in $lines) {
        if ($line -match "^\[[^\]]+\]\s+(\d+):\s+$([regex]::Escape($nickname)), Score: ([0-9]+), Captured: ([0-9]+)") {
            return [pscustomobject]@{
                Placement = [int]$matches[1]
                Score = [int]$matches[2]
                Captured = [int]$matches[3]
            }
        }
    }
    return $null
}

$engineDll = (Resolve-Path (Join-Path $repoRoot "engine\Zooscape\bin\Debug\net8.0\Zooscape.dll")).Path
$botADllPath = Resolve-Dll $BotADll $BotAName
$botBDllPath = Resolve-Dll $BotBDll $BotBName

$results = @()
foreach ($seed in $Seeds) {
    $seedDir = Join-Path $runRoot ("seed_" + $seed)
    New-Item -ItemType Directory -Force -Path $seedDir | Out-Null
    $port = Get-FreeTcpPort $BasePort

    $engineEnv = @{
        ASPNETCORE_ENVIRONMENT = "Development"
        ASPNETCORE_URLS = "http://localhost:$port"
        SignalR__Port = "$port"
        LOG_DIR = $seedDir
        GameSettings__NumberOfBots = $NumberOfBots
        GameSettings__MaxTicks = $MaxTicks
        GameSettings__StartGameTimeout = $StartGameTimeout
        GameSettings__TickDuration = $TickDuration
        GameSettings__Seed = $seed
        GameLogsConfiguration__PushLogsToS3 = "false"
    }

    $botAEnv = @{
        BOT_NICKNAME = $BotAName
        Token = [guid]::NewGuid().ToString()
        BOT_TOKEN = [guid]::NewGuid().ToString()
        RUNNER_IPV4 = "http://localhost"
        RUNNER_PORT = "$port"
        HUB_NAME = "bothub"
    }

    $botBEnv = @{
        BOT_NICKNAME = $BotBName
        Token = [guid]::NewGuid().ToString()
        BOT_TOKEN = [guid]::NewGuid().ToString()
        RUNNER_IPV4 = "http://localhost"
        RUNNER_PORT = "$port"
        HUB_NAME = "bothub"
    }

    $engine = $null
    $botA = $null
    $botB = $null

    try {
        $engine = Start-LoggedProcess "dotnet" "`"$engineDll`"" (Split-Path $engineDll -Parent) $engineEnv (Join-Path $seedDir "engine.out.log") (Join-Path $seedDir "engine.err.log")
        Start-Sleep -Seconds 2
        $botA = Start-LoggedProcess "dotnet" "`"$botADllPath`"" (Split-Path $botADllPath -Parent) $botAEnv (Join-Path $seedDir "$BotAName.out.log") (Join-Path $seedDir "$BotAName.err.log")
        $botB = Start-LoggedProcess "dotnet" "`"$botBDllPath`"" (Split-Path $botBDllPath -Parent) $botBEnv (Join-Path $seedDir "$BotBName.out.log") (Join-Path $seedDir "$BotBName.err.log")

        if (-not $engine.WaitForExit(((($MaxTicks * $TickDuration) / 1000) + 60) * 1000)) {
            throw "Engine timed out for seed $seed"
        }

        Start-Sleep -Milliseconds 500
        $engineLog = Get-Content (Join-Path $seedDir "engine.out.log")
        $aPlacement = Get-Placement $engineLog $BotAName
        $bPlacement = Get-Placement $engineLog $BotBName
        if ($null -eq $aPlacement -or $null -eq $bPlacement) {
            throw "Could not parse placements for seed $seed"
        }

        $winner = if ($aPlacement.Score -gt $bPlacement.Score) { $BotAName } elseif ($bPlacement.Score -gt $aPlacement.Score) { $BotBName } else { "Tie" }
        $result = [pscustomobject]@{
            Seed = $seed
            BotAPlacement = $aPlacement.Placement
            BotAScore = $aPlacement.Score
            BotACaptured = $aPlacement.Captured
            BotBPlacement = $bPlacement.Placement
            BotBScore = $bPlacement.Score
            BotBCaptured = $bPlacement.Captured
            Winner = $winner
        }
        $results += $result
        Write-Host ("Seed {0}: {1} {2} ({3} captures) vs {4} {5} ({6} captures) -> {7}" -f
            $seed,
            $BotAName,
            $aPlacement.Score,
            $aPlacement.Captured,
            $BotBName,
            $bPlacement.Score,
            $bPlacement.Captured,
            $winner)
    }
    finally {
        Stop-LoggedProcess $botA
        Stop-LoggedProcess $botB
        Stop-LoggedProcess $engine
    }
}

$summary = [pscustomobject]@{
    RunName = $RunName
    BotA = $BotAName
    BotB = $BotBName
    Wins = ($results | Where-Object Winner -eq $BotAName).Count
    Losses = ($results | Where-Object Winner -eq $BotBName).Count
    Ties = ($results | Where-Object Winner -eq "Tie").Count
    AvgBotAScore = [Math]::Round((($results | Measure-Object BotAScore -Average).Average), 2)
    AvgBotACaptured = [Math]::Round((($results | Measure-Object BotACaptured -Average).Average), 2)
    AvgBotBScore = [Math]::Round((($results | Measure-Object BotBScore -Average).Average), 2)
    AvgBotBCaptured = [Math]::Round((($results | Measure-Object BotBCaptured -Average).Average), 2)
    Results = $results
}

$summary | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $runRoot "summary.json")
$results | Format-Table -AutoSize
