param(
    [int[]]$Seeds = @(101, 102, 103),
    [string]$CandidateProject = "Bots\MonteCarloBot\NeuralNetBot\NeuralNetBot.csproj",
    [string]$CandidateNickname = "NeuralNetBot",
    [string]$OpponentBot = "MonteCarloBot",
    [int]$MaxTicks = 450,
    [int]$StartGameTimeout = 20,
    [int]$TickDuration = 200,
    [int]$BasePort = 5000,
    [switch]$Release
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$configuration = if ($Release) { "Release" } else { "Debug" }
$tfm = "net8.0"
$stageInstance = Get-Date -Format "yyyyMMdd_HHmmss_fff"
$outputRoot = Join-Path $repoRoot "output\headtohead"
$bestRoot = Join-Path $repoRoot "output\best-bot"
$dotnetHome = Join-Path $repoRoot "output\dotnet-home"
$nugetConfig = Join-Path $repoRoot "tools\nuget.local.config"
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
New-Item -ItemType Directory -Force -Path $bestRoot | Out-Null
New-Item -ItemType Directory -Force -Path $dotnetHome | Out-Null

$env:DOTNET_CLI_HOME = $dotnetHome
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = "0"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"
$env:MSBuildEnableWorkloadResolver = "false"
$env:HOME = $dotnetHome
$env:USERPROFILE = $dotnetHome
$env:APPDATA = $repoRoot
$env:NUGET_PACKAGES = "C:\Users\marvi\.nuget\packages"

function Invoke-Build([string]$projectPath, [string]$workingDirectory) {
    Write-Host "Building $projectPath ($configuration)..."
    Push-Location $workingDirectory
    try {
        dotnet build (Split-Path $projectPath -Leaf) -c $configuration --nologo --configfile $nugetConfig -p:RestoreIgnoreFailedSources=true
    }
    finally {
        Pop-Location
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed for $projectPath"
    }
}

function Ensure-RuntimeDependency([string]$targetDirectory, [string]$fileName, [string[]]$sourceCandidates) {
    $targetPath = Join-Path $targetDirectory $fileName
    if (Test-Path $targetPath) {
        return
    }

    foreach ($source in $sourceCandidates) {
        if (-not [string]::IsNullOrWhiteSpace($source) -and (Test-Path $source)) {
            Copy-Item -LiteralPath $source -Destination $targetPath -Force
            return
        }
    }
}

function Copy-DirectoryContents([string]$sourceDirectory, [string]$targetDirectory) {
    if (-not (Test-Path $sourceDirectory)) {
        return
    }

    New-Item -ItemType Directory -Force -Path $targetDirectory | Out-Null
    Copy-Item -Path (Join-Path $sourceDirectory '*') -Destination $targetDirectory -Recurse -Force
}

function Get-ProjectDll([string]$projectDirectory, [string]$assemblyName) {
    $binPath = Join-Path $projectDirectory "bin\$configuration\$tfm\$assemblyName.dll"
    $objPath = Join-Path $projectDirectory "obj\$configuration\$tfm\$assemblyName.dll"
    $stagePath = Join-Path $repoRoot "output\bot-staging\$assemblyName\$configuration\$tfm\$stageInstance\$assemblyName.dll"
    $stageDir = Split-Path $stagePath -Parent
    $stageRoot = Join-Path $repoRoot "output\bot-staging\$assemblyName\$configuration\$tfm"
    $latestStageDll = $null

    if (Test-Path $stageRoot) {
        $latestStageDll = Get-ChildItem $stageRoot -Recurse -Filter "$assemblyName.dll" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1 -ExpandProperty FullName
    }

    if (-not (Test-Path $binPath) -and -not [string]::IsNullOrWhiteSpace($latestStageDll)) {
        return $latestStageDll
    }

    if (-not (Test-Path $binPath)) {
        if (-not [string]::IsNullOrWhiteSpace($latestStageDll)) {
            return $latestStageDll
        }

        throw "Bot dll not found: $binPath"
    }

    New-Item -ItemType Directory -Force -Path $stageDir | Out-Null
    try {
        Copy-Item -Path (Join-Path $projectDirectory "bin\$configuration\$tfm\*") -Destination $stageDir -Recurse -Force
    }
    catch {
        Write-Warning "Could not fully stage $assemblyName from bin output. Falling back to bin directory. $($_.Exception.Message)"
        return $binPath
    }

    if (Test-Path $objPath) {
        $objItem = Get-Item $objPath
        $binItem = Get-Item $binPath
        if ($objItem.LastWriteTimeUtc -gt $binItem.LastWriteTimeUtc) {
            try {
                Copy-Item -LiteralPath $objPath -Destination $stagePath -Force

                $objPdbPath = [System.IO.Path]::ChangeExtension($objPath, ".pdb")
                if (Test-Path $objPdbPath) {
                    Copy-Item -LiteralPath $objPdbPath -Destination (Join-Path $stageDir "$assemblyName.pdb") -Force
                }
            }
            catch {
                Write-Warning "Could not overlay fresher obj output for $assemblyName. Falling back to bin directory. $($_.Exception.Message)"
                return $binPath
            }
        }
    }

    return $stagePath
}

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
            -WindowStyle Hidden `
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

function Get-LastObservedTick([string[]]$lines) {
    $lastTick = $null
    foreach ($line in $lines) {
        if ($line -match "\bGame tick (\d+)\b") {
            $lastTick = [int]$matches[1]
        }
    }

    return $lastTick
}

function Get-ProcessExitCode($loggedProcess) {
    if ($null -eq $loggedProcess) {
        return $null
    }

    try {
        if ($loggedProcess.Process.HasExited) {
            return $loggedProcess.Process.ExitCode
        }
    }
    catch {
    }

    return $null
}

function Get-AverageOrZero([object[]]$values) {
    if ($null -eq $values -or $values.Count -eq 0) {
        return 0.0
    }

    return [Math]::Round((($values | Measure-Object -Average).Average), 2)
}

function Get-CandidateSummary([object[]]$results, [string]$candidateName, [string]$opponentName, [string]$runName) {
    $completedResults = @($results | Where-Object { -not $_.Error })
    $errorSeeds = @($results | Where-Object { $_.Error } | ForEach-Object { $_.Seed })
    $wins = 0
    $losses = 0
    $ties = 0
    $candidateScores = @()
    $opponentScores = @()
    $candidateCaptures = @()
    $opponentCaptures = @()

    foreach ($result in $completedResults) {
        $candidateScores += $result.CandidateScore
        $opponentScores += $result.OpponentScore
        $candidateCaptures += $result.CandidateCaptured
        $opponentCaptures += $result.OpponentCaptured

        if ($result.Winner -eq $candidateName) {
            $wins++
        }
        elseif ($result.Winner -eq $opponentName) {
            $losses++
        }
        else {
            $ties++
        }
    }

    return [pscustomobject]@{
        CandidateBot = $candidateName
        OpponentBot = $opponentName
        RunName = $runName
        Seeds = $results.Seed
        CompletedSeeds = $completedResults.Seed
        ErrorSeeds = $errorSeeds
        HadErrors = $errorSeeds.Count -gt 0
        Wins = $wins
        Losses = $losses
        Ties = $ties
        AvgScore = Get-AverageOrZero $candidateScores
        AvgOpponentScore = Get-AverageOrZero $opponentScores
        AvgCaptured = Get-AverageOrZero $candidateCaptures
        AvgOpponentCaptured = Get-AverageOrZero $opponentCaptures
        AvgScoreDiff = [Math]::Round((Get-AverageOrZero $candidateScores) - (Get-AverageOrZero $opponentScores), 2)
        AvgCaptureDiff = [Math]::Round((Get-AverageOrZero $candidateCaptures) - (Get-AverageOrZero $opponentCaptures), 2)
        Commit = (git rev-parse HEAD).Trim()
        CommitShort = (git rev-parse --short HEAD).Trim()
        RecordedAt = (Get-Date).ToString("o")
    }
}

function Test-IsBetterSummary($candidate, $best) {
    if ($candidate.PSObject.Properties.Name -contains "HadErrors" -and $candidate.HadErrors) {
        return $false
    }

    if ($null -eq $best) {
        return $true
    }

    if ($best.PSObject.Properties.Name -contains "HadErrors" -and $best.HadErrors) {
        return $true
    }

    if ($candidate.Wins -ne $best.Wins) {
        return $candidate.Wins -gt $best.Wins
    }

    if ($candidate.AvgScoreDiff -ne $best.AvgScoreDiff) {
        return $candidate.AvgScoreDiff -gt $best.AvgScoreDiff
    }

    if ($candidate.AvgCaptured -ne $best.AvgCaptured) {
        return $candidate.AvgCaptured -lt $best.AvgCaptured
    }

    return $candidate.AvgScore -gt $best.AvgScore
}

$opponentProjectPath = Join-Path $repoRoot "Bots\$OpponentBot\$OpponentBot.csproj"
$candidateProjectPath = Join-Path $repoRoot $CandidateProject
$candidateProjectDir = Split-Path $candidateProjectPath -Parent
$opponentProjectDir = Join-Path $repoRoot "Bots\$OpponentBot"
if (-not (Test-Path $candidateProjectPath)) {
    throw "Candidate project not found: $candidateProjectPath"
}

$engineDll = Join-Path $repoRoot "engine\Zooscape\bin\$configuration\$tfm\Zooscape.dll"
$opponentBinDll = Join-Path $opponentProjectDir "bin\$configuration\$tfm\$OpponentBot.dll"
$opponentStageRoot = Join-Path $repoRoot "output\bot-staging\$OpponentBot\$configuration\$tfm"
$opponentStageDll = if (Test-Path $opponentStageRoot) {
    Get-ChildItem $opponentStageRoot -Recurse -Filter "$OpponentBot.dll" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}
else {
    $null
}
$functionalTestsBin = Join-Path $repoRoot "FunctionalTests\bin\$configuration\$tfm"
$neuralNetBin = Join-Path $repoRoot "Bots\MonteCarloBot\NeuralNetBot\bin\$configuration\$tfm"

if ($OpponentBot -eq "MonteCarloBot" -and -not (Test-Path $opponentBinDll) -and -not (Test-Path $opponentStageDll)) {
    if (Test-Path (Join-Path $functionalTestsBin "MonteCarloBot.dll")) {
        Copy-DirectoryContents $functionalTestsBin (Join-Path $opponentProjectDir "bin\$configuration\$tfm")
    }
    elseif (Test-Path (Join-Path $neuralNetBin "MonteCarloBot.dll")) {
        Copy-DirectoryContents $neuralNetBin (Join-Path $opponentProjectDir "bin\$configuration\$tfm")
    }
}

if (-not (Test-Path $opponentBinDll) -and -not (Test-Path $opponentStageDll)) {
    Invoke-Build $opponentProjectPath $opponentProjectDir
}
if (-not (Test-Path $engineDll)) {
    Invoke-Build (Join-Path $repoRoot "engine\Zooscape\Zooscape.csproj") (Join-Path $repoRoot "engine\Zooscape")
}
Invoke-Build $candidateProjectPath $candidateProjectDir

if ($OpponentBot -eq "MonteCarloBot" -and -not (Test-Path $opponentBinDll) -and -not (Test-Path $opponentStageDll)) {
    if (Test-Path (Join-Path $functionalTestsBin "MonteCarloBot.dll")) {
        Copy-DirectoryContents $functionalTestsBin (Join-Path $opponentProjectDir "bin\$configuration\$tfm")
    }
    elseif (Test-Path (Join-Path $neuralNetBin "MonteCarloBot.dll")) {
        Copy-DirectoryContents $neuralNetBin (Join-Path $opponentProjectDir "bin\$configuration\$tfm")
    }
}

$candidateDll = Get-ProjectDll $candidateProjectDir $CandidateNickname
$opponentDll = Get-ProjectDll $opponentProjectDir $OpponentBot

$candidateDir = Split-Path $candidateDll -Parent
$opponentDir = Split-Path $opponentDll -Parent
$staticHeuroSources = @(
    (Join-Path $repoRoot "Bots\StaticHeuro\bin\$configuration\$tfm\StaticHeuro.dll"),
    (Join-Path $repoRoot "FunctionalTests\bin\$configuration\$tfm\StaticHeuro.dll"),
    (Join-Path $candidateDir "StaticHeuro.dll")
)
$staticHeuroPdbSources = @(
    (Join-Path $repoRoot "Bots\StaticHeuro\bin\$configuration\$tfm\StaticHeuro.pdb"),
    (Join-Path $repoRoot "FunctionalTests\bin\$configuration\$tfm\StaticHeuro.pdb"),
    (Join-Path $candidateDir "StaticHeuro.pdb")
)

if ($OpponentBot -eq "MonteCarloBot") {
    Ensure-RuntimeDependency $opponentDir "StaticHeuro.dll" $staticHeuroSources
    Ensure-RuntimeDependency $opponentDir "StaticHeuro.pdb" $staticHeuroPdbSources
}

$runName = "{0}_{1}_vs_{2}" -f (Get-Date -Format "yyyyMMdd_HHmmss"), $CandidateNickname, $OpponentBot
$runRoot = Join-Path $outputRoot $runName
New-Item -ItemType Directory -Force -Path $runRoot | Out-Null

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
        GameSettings__NumberOfBots = 2
        GameSettings__MaxTicks = $MaxTicks
        GameSettings__StartGameTimeout = $StartGameTimeout
        GameSettings__TickDuration = $TickDuration
        GameSettings__Seed = $seed
        GameLogsConfiguration__PushLogsToS3 = "false"
    }

    $candidateEnv = @{
        BOT_NICKNAME = $CandidateNickname
        Token = [guid]::NewGuid().ToString()
        BOT_TOKEN = [guid]::NewGuid().ToString()
        RUNNER_IPV4 = "http://localhost"
        RUNNER_PORT = "$port"
        HUB_NAME = "bothub"
    }

    $opponentEnv = @{
        BOT_NICKNAME = $OpponentBot
        Token = [guid]::NewGuid().ToString()
        BOT_TOKEN = [guid]::NewGuid().ToString()
        RUNNER_IPV4 = "http://localhost"
        RUNNER_PORT = "$port"
        HUB_NAME = "bothub"
    }

    $engine = $null
    $candidateProc = $null
    $opponentProc = $null

    try {
        $engine = Start-LoggedProcess "engine" "dotnet" "`"$engineDll`"" (Split-Path $engineDll -Parent) $engineEnv (Join-Path $seedDir "engine.out.log") (Join-Path $seedDir "engine.err.log")
        Start-Sleep -Seconds 2
        $candidateProc = Start-LoggedProcess $CandidateNickname "dotnet" "`"$candidateDll`"" (Split-Path $candidateDll -Parent) $candidateEnv (Join-Path $seedDir "$CandidateNickname.out.log") (Join-Path $seedDir "$CandidateNickname.err.log")
        $opponentProc = Start-LoggedProcess $OpponentBot "dotnet" "`"$opponentDll`"" (Split-Path $opponentDll -Parent) $opponentEnv (Join-Path $seedDir "$OpponentBot.out.log") (Join-Path $seedDir "$OpponentBot.err.log")

        if (-not $engine.Process.WaitForExit(((($MaxTicks * $TickDuration) / 1000) + 60) * 1000)) {
            throw "Engine timed out for seed $seed"
        }

        Start-Sleep -Milliseconds 500
        $engineLog = Get-Content (Join-Path $seedDir "engine.out.log")
        $candidatePlacement = Get-Placement $engineLog $CandidateNickname
        $opponentPlacement = Get-Placement $engineLog $OpponentBot
        if ($null -eq $candidatePlacement -or $null -eq $opponentPlacement) {
            throw "Could not parse placements for seed $seed from engine log"
        }

        $engineExitCode = Get-ProcessExitCode $engine
        $candidateExitCode = Get-ProcessExitCode $candidateProc
        $opponentExitCode = Get-ProcessExitCode $opponentProc
        $lastObservedTick = Get-LastObservedTick $engineLog

        $winner = if ($candidatePlacement.Score -gt $opponentPlacement.Score) {
            $CandidateNickname
        }
        elseif ($opponentPlacement.Score -gt $candidatePlacement.Score) {
            $OpponentBot
        }
        else {
            "Tie"
        }

        $result = [pscustomobject]@{
            Seed = $seed
            CandidateScore = $candidatePlacement.Score
            CandidateCaptured = $candidatePlacement.Captured
            OpponentScore = $opponentPlacement.Score
            OpponentCaptured = $opponentPlacement.Captured
            Winner = $winner
            LastObservedTick = $lastObservedTick
            EngineExitCode = $engineExitCode
            CandidateExitCode = $candidateExitCode
            OpponentExitCode = $opponentExitCode
            Error = $null
        }
        $results += $result
        Write-Host ("Seed {0}: {1} {2} ({3} captures) vs {4} {5} ({6} captures) -> {7}" -f
            $seed,
            $CandidateNickname,
            $candidatePlacement.Score,
            $candidatePlacement.Captured,
            $OpponentBot,
            $opponentPlacement.Score,
            $opponentPlacement.Captured,
            $winner)
    }
    catch {
        $engineLogPath = Join-Path $seedDir "engine.out.log"
        $engineLog = if (Test-Path $engineLogPath) { @(Get-Content $engineLogPath) } else { @() }
        $errorMessage = $_.Exception.Message
        $lastObservedTick = Get-LastObservedTick $engineLog
        $engineExitCode = Get-ProcessExitCode $engine
        $candidateExitCode = Get-ProcessExitCode $candidateProc
        $opponentExitCode = Get-ProcessExitCode $opponentProc

        $results += [pscustomobject]@{
            Seed = $seed
            CandidateScore = $null
            CandidateCaptured = $null
            OpponentScore = $null
            OpponentCaptured = $null
            Winner = "Error"
            LastObservedTick = $lastObservedTick
            EngineExitCode = $engineExitCode
            CandidateExitCode = $candidateExitCode
            OpponentExitCode = $opponentExitCode
            Error = $errorMessage
        }

        Write-Warning ("Seed {0} failed: {1} (last tick: {2}; exit codes engine/candidate/opponent: {3}/{4}/{5})" -f
            $seed,
            $errorMessage,
            $(if ($null -eq $lastObservedTick) { "n/a" } else { $lastObservedTick }),
            $(if ($null -eq $engineExitCode) { "running" } else { $engineExitCode }),
            $(if ($null -eq $candidateExitCode) { "running" } else { $candidateExitCode }),
            $(if ($null -eq $opponentExitCode) { "running" } else { $opponentExitCode }))
    }
    finally {
        Stop-LoggedProcess $candidateProc
        Stop-LoggedProcess $opponentProc
        Stop-LoggedProcess $engine
    }
}

$summary = Get-CandidateSummary $results $CandidateNickname $OpponentBot $runName
$summaryPath = Join-Path $runRoot "summary.json"
$summary | ConvertTo-Json -Depth 5 | Set-Content $summaryPath

$bestPath = Join-Path $bestRoot "$($CandidateNickname)_vs_$($OpponentBot).json"
$bestSummary = $null
if (Test-Path $bestPath) {
    $bestSummary = Get-Content $bestPath -Raw | ConvertFrom-Json
}

if (Test-IsBetterSummary $summary $bestSummary) {
    $summary | ConvertTo-Json -Depth 5 | Set-Content $bestPath
    $tagName = "best-{0}-vs-{1}-{2}" -f $CandidateNickname.ToLowerInvariant(), $OpponentBot.ToLowerInvariant(), (Get-Date -Format "yyyyMMdd-HHmmss")
    git tag $tagName
    Write-Host "New best run recorded. Created git tag: $tagName"
}
else {
    Write-Host "Run completed, but it did not beat the recorded best."
}

$summary
