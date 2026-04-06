param(
    [int[]]$Seeds = @(101, 102, 103, 104, 105),
    [string]$BotA = "MonteCarloBot",
    [string]$BotB = "ClingyHeuroBot2",
    [int]$NumberOfBots = 2,
    [int]$MaxTicks = 2000,
    [int]$StartGameTimeout = 20,
    [int]$TickDuration = 200,
    [int]$BasePort = 5000,
    [string]$RunName = "",
    [string]$OutputRoot = "",
    [switch]$Release
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$configuration = if ($Release) { "Release" } else { "Debug" }
$tfm = "net8.0"
$safeBotA = ($BotA -replace '[^A-Za-z0-9_-]', '_')
$safeBotB = ($BotB -replace '[^A-Za-z0-9_-]', '_')
if ([string]::IsNullOrWhiteSpace($RunName)) {
    $RunName = "{0}_{1}_vs_{2}" -f (Get-Date -Format "yyyyMMdd_HHmmss"), $safeBotA, $safeBotB
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $preferredRoot = Join-Path $repoRoot "logs\headtohead"
    $fallbackRoot = Join-Path $repoRoot "output\headtohead"

    try {
        New-Item -ItemType Directory -Force -Path $preferredRoot -ErrorAction Stop | Out-Null
        $OutputRoot = $preferredRoot
    }
    catch {
        Write-Warning "Could not write to $preferredRoot. Falling back to $fallbackRoot."
        New-Item -ItemType Directory -Force -Path $fallbackRoot -ErrorAction Stop | Out-Null
        $OutputRoot = $fallbackRoot
    }
}
else {
    New-Item -ItemType Directory -Force -Path $OutputRoot -ErrorAction Stop | Out-Null
}

$runRoot = Join-Path $OutputRoot $RunName
New-Item -ItemType Directory -Force -Path $runRoot -ErrorAction Stop | Out-Null

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

function Get-BotDll([string]$botName) {
    $binPath = Join-Path $repoRoot "Bots\$botName\bin\$configuration\$tfm\$botName.dll"
    $objPath = Join-Path $repoRoot "Bots\$botName\obj\$configuration\$tfm\$botName.dll"

    if (Test-Path $objPath) {
        if (-not (Test-Path $binPath)) {
            throw "Bot bin output not found for staging: $binPath"
        }

        $objItem = Get-Item $objPath
        $binItem = Get-Item $binPath
        if ($objItem.LastWriteTimeUtc -gt $binItem.LastWriteTimeUtc) {
            $stageDir = Join-Path $repoRoot "output\bot-staging\$botName\$configuration\$tfm"
            New-Item -ItemType Directory -Force -Path $stageDir -ErrorAction Stop | Out-Null
            Copy-Item -Path (Join-Path $repoRoot "Bots\$botName\bin\$configuration\$tfm\*") -Destination $stageDir -Recurse -Force
            Copy-Item -LiteralPath $objPath -Destination (Join-Path $stageDir "$botName.dll") -Force

            $objPdbPath = [System.IO.Path]::ChangeExtension($objPath, ".pdb")
            if (Test-Path $objPdbPath) {
                Copy-Item -LiteralPath $objPdbPath -Destination (Join-Path $stageDir "$botName.pdb") -Force
            }

            return (Join-Path $stageDir "$botName.dll")
        }
    }

    if (-not (Test-Path $binPath)) {
        throw "Bot dll not found: $binPath"
    }

    return $binPath
}

function Start-LoggedProcess(
    [string]$name,
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
        $process = Start-Process -FilePath $filePath `
            -ArgumentList $arguments `
            -WorkingDirectory $workingDirectory `
            -RedirectStandardOutput $stdoutPath `
            -RedirectStandardError $stderrPath `
            -PassThru
    }
    finally {
        foreach ($key in $environment.Keys) {
            [Environment]::SetEnvironmentVariable($key, $previousValues[$key], "Process")
        }
    }

    return [pscustomobject]@{
        Name = $name
        Process = $process
    }
}

function Stop-LoggedProcess($loggedProcess) {
    if ($null -eq $loggedProcess) { return }
    try {
        if (-not $loggedProcess.Process.HasExited) {
            $loggedProcess.Process.Kill($true)
            $loggedProcess.Process.WaitForExit(5000) | Out-Null
        }
    } catch {}
    try { $loggedProcess.Process.Dispose() } catch {}
}

function Get-Placement([string[]]$lines, [string]$nickname) {
    foreach ($line in $lines) {
        if ($line -match "^\[[^\]]+\]\s+(\d+):\s+$([regex]::Escape($nickname)), Score: ([0-9]+)") {
            return [pscustomobject]@{
                Placement = [int]$matches[1]
                Score = [int]$matches[2]
            }
        }
    }
    return $null
}

$engineDll = Join-Path $repoRoot "engine\Zooscape\bin\$configuration\$tfm\Zooscape.dll"
if (-not (Test-Path $engineDll)) {
    throw "Engine dll not found: $engineDll"
}

$botADll = Get-BotDll $BotA
$botBDll = Get-BotDll $BotB

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
        BOT_NICKNAME = $BotA
        Token = [guid]::NewGuid().ToString()
        BOT_TOKEN = [guid]::NewGuid().ToString()
        RUNNER_IPV4 = "http://localhost"
        RUNNER_PORT = "$port"
        HUB_NAME = "bothub"
    }

    $botBEnv = @{
        BOT_NICKNAME = $BotB
        Token = [guid]::NewGuid().ToString()
        BOT_TOKEN = [guid]::NewGuid().ToString()
        RUNNER_IPV4 = "http://localhost"
        RUNNER_PORT = "$port"
        HUB_NAME = "bothub"
    }

    $engine = $null
    $botAProc = $null
    $botBProc = $null

    try {
        $engine = Start-LoggedProcess "engine" "dotnet" "`"$engineDll`"" (Split-Path $engineDll -Parent) $engineEnv (Join-Path $seedDir "engine.out.log") (Join-Path $seedDir "engine.err.log")
        Start-Sleep -Seconds 2
        $botAProc = Start-LoggedProcess $BotA "dotnet" "`"$botADll`"" (Split-Path $botADll -Parent) $botAEnv (Join-Path $seedDir "$BotA.out.log") (Join-Path $seedDir "$BotA.err.log")
        $botBProc = Start-LoggedProcess $BotB "dotnet" "`"$botBDll`"" (Split-Path $botBDll -Parent) $botBEnv (Join-Path $seedDir "$BotB.out.log") (Join-Path $seedDir "$BotB.err.log")

        if (-not $engine.Process.WaitForExit(((($MaxTicks * $TickDuration) / 1000) + 60) * 1000)) {
            throw "Engine timed out for seed $seed"
        }

        Start-Sleep -Milliseconds 500
        $engineLog = Get-Content (Join-Path $seedDir "engine.out.log")
        $aPlacement = Get-Placement $engineLog $BotA
        $bPlacement = Get-Placement $engineLog $BotB
        if ($null -eq $aPlacement -or $null -eq $bPlacement) {
            throw "Could not parse placements for seed $seed from engine log"
        }

        $winner = if ($aPlacement.Score -gt $bPlacement.Score) { $BotA } elseif ($bPlacement.Score -gt $aPlacement.Score) { $BotB } else { "Tie" }
        $result = [pscustomobject]@{
            Seed = $seed
            BotAPlacement = $aPlacement.Placement
            BotAScore = $aPlacement.Score
            BotBPlacement = $bPlacement.Placement
            BotBScore = $bPlacement.Score
            Winner = $winner
            LogDir = $seedDir
        }
        $results += $result
        Write-Host ("Seed {0} on port {1}: {2} {3} vs {4} {5} -> {6}" -f $seed, $port, $BotA, $aPlacement.Score, $BotB, $bPlacement.Score, $winner)
    }
    finally {
        Stop-LoggedProcess $botAProc
        Stop-LoggedProcess $botBProc
        Stop-LoggedProcess $engine
    }
}

$results | Format-Table -AutoSize
