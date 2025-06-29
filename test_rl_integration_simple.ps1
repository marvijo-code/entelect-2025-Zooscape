# Test RL Bot Integration - Simple Version
# Quick verification that everything is set up correctly

Write-Host "Testing RL Bot Integration Setup" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$success = $true

# Test 1: Check if main script exists
Write-Host "1. Checking main script..." -ForegroundColor Yellow
$mainScript = Join-Path $scriptRoot "ra-run-all-local.ps1"
if (Test-Path $mainScript) {
    Write-Host "   OK: ra-run-all-local.ps1 found" -ForegroundColor Green
} else {
    Write-Host "   ERROR: ra-run-all-local.ps1 not found" -ForegroundColor Red
    $success = $false
}

# Test 2: Check RL bot directory
Write-Host "2. Checking RL bot directory..." -ForegroundColor Yellow
$rlBotDir = Join-Path $scriptRoot "Bots\rl"
if (Test-Path $rlBotDir) {
    Write-Host "   OK: Bots\rl directory found" -ForegroundColor Green
} else {
    Write-Host "   ERROR: Bots\rl directory not found" -ForegroundColor Red
    $success = $false
}

# Test 3: Check Python virtual environment
Write-Host "3. Checking Python virtual environment..." -ForegroundColor Yellow
$venvPath = Join-Path $rlBotDir ".venv"
if (Test-Path $venvPath) {
    Write-Host "   OK: Python virtual environment found" -ForegroundColor Green
} else {
    Write-Host "   ERROR: Python virtual environment not found at $venvPath" -ForegroundColor Red
    Write-Host "   TIP: Run: cd Bots\rl && python -m venv .venv" -ForegroundColor Yellow
    $success = $false
}

# Test 4: Check play bot runner script
Write-Host "4. Checking play bot runner..." -ForegroundColor Yellow
$playBotScript = Join-Path $rlBotDir "play_bot_runner.py"
if (Test-Path $playBotScript) {
    Write-Host "   OK: play_bot_runner.py found" -ForegroundColor Green
} else {
    Write-Host "   ERROR: play_bot_runner.py not found" -ForegroundColor Red
    $success = $false
}

# Test 5: Check trained models
Write-Host "5. Checking trained models..." -ForegroundColor Yellow
$modelsDir = Join-Path $rlBotDir "models"
if (Test-Path $modelsDir) {
    $modelFiles = Get-ChildItem -Path $modelsDir -Filter "*.weights.h5" -ErrorAction SilentlyContinue
    if ($modelFiles.Count -gt 0) {
        Write-Host "   OK: Found $($modelFiles.Count) trained model(s)" -ForegroundColor Green
        foreach ($model in $modelFiles) {
            Write-Host "      FILE: $($model.Name)" -ForegroundColor DarkGray
        }
    } else {
        Write-Host "   WARNING: No trained models found in models directory" -ForegroundColor Yellow
        Write-Host "   TIP: Run training first: cd Bots\rl && .venv\Scripts\python.exe train_with_real_logs.py" -ForegroundColor Yellow
    }
} else {
    Write-Host "   ERROR: Models directory not found" -ForegroundColor Red
    $success = $false
}

# Summary
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
if ($success) {
    Write-Host "SUCCESS: All critical components are ready!" -ForegroundColor Green
    Write-Host "READY: You can now run: .\ra-run-all-local.ps1" -ForegroundColor Green
} else {
    Write-Host "ERROR: Some components are missing. Please fix the issues above." -ForegroundColor Red
    Write-Host "HELP: See README_ra_integration.md for setup instructions" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Your RL bot will compete against multiple opponents and track scores automatically!" -ForegroundColor Cyan 