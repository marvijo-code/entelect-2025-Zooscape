#!/usr/bin/env pwsh
# rt-run-tests.ps1 - Run FunctionalTests with proper setup
# Quick test runner for Zooscape functional tests

param(
    [string]$Configuration = "Debug",
    [string]$Verbosity = "normal",
    [switch]$NoBuild,
    [switch]$Watch,
    [string]$Filter = ""
)

Write-Host "🧪 Zooscape Functional Test Runner" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan

# Check if we're in the right directory
if (-not (Test-Path "FunctionalTests/FunctionalTests.csproj")) {
    Write-Error "❌ FunctionalTests project not found. Please run this script from the solution root."
    exit 1
}

try {
    # Ensure GameStates directory exists in output
    $outputPath = "FunctionalTests/bin/$Configuration/net8.0/GameStates"
    if (-not (Test-Path $outputPath)) {
        Write-Host "📁 Creating GameStates output directory..." -ForegroundColor Yellow
        New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
    }

    # Copy test data files if they exist
    $sourceGameStates = "FunctionalTests/GameStates"
    if (Test-Path $sourceGameStates) {
        Write-Host "📋 Copying test data files..." -ForegroundColor Yellow
        $testFiles = Get-ChildItem "$sourceGameStates/*.json" -ErrorAction SilentlyContinue
        foreach ($file in $testFiles) {
            Copy-Item $file.FullName $outputPath -Force
            Write-Host "   ✓ Copied $($file.Name)" -ForegroundColor Green
        }
    }

    # Build test command
    $testCommand = @("test", "FunctionalTests/FunctionalTests.csproj", "--verbosity", $Verbosity)
    
    if ($NoBuild) {
        $testCommand += "--no-build"
    }
    
    if ($Configuration -ne "Debug") {
        $testCommand += @("--configuration", $Configuration)
    }
    
    if ($Filter) {
        $testCommand += @("--filter", $Filter)
    }

    # Run tests
    Write-Host "🚀 Running functional tests..." -ForegroundColor Blue
    Write-Host "Command: dotnet $($testCommand -join ' ')" -ForegroundColor Gray
    Write-Host ""

    if ($Watch) {
        # For watch mode, use a different approach
        $watchCommand = @("watch", "test", "FunctionalTests/FunctionalTests.csproj", "--verbosity", $Verbosity)
        if ($Filter) {
            $watchCommand += @("--filter", $Filter)
        }
        & dotnet @watchCommand
    } else {
        & dotnet @testCommand
    }

    $exitCode = $LASTEXITCODE

    if ($exitCode -eq 0) {
        Write-Host ""
        Write-Host "✅ All tests passed!" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "❌ Some tests failed. Exit code: $exitCode" -ForegroundColor Red
    }

    exit $exitCode

} catch {
    Write-Host ""
    Write-Host "❌ Error running tests: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Usage examples:
# .\rt-run-tests.ps1                           # Run all tests
# .\rt-run-tests.ps1 -Verbosity detailed       # Run with detailed output
# .\rt-run-tests.ps1 -Filter "GameState34"     # Run only tests matching filter
# .\rt-run-tests.ps1 -Watch                    # Run in watch mode
# .\rt-run-tests.ps1 -Configuration Release    # Run release build tests 