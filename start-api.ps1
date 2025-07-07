#!/usr/bin/env pwsh
# Script to check and start the FunctionalTests API

param(
    [switch]$Force,
    [switch]$Stop,
    [string]$Port = "5008"
)

$ApiName = "FunctionalTests API"
$ProjectPath = "FunctionalTests"
$Url = "http://localhost:$Port"

function Test-ApiRunning {
    param([string]$TestPort)
    
    try {
        # Check if port is in use using netstat (more reliable)
        $netstatOutput = netstat -an | Select-String ":$TestPort.*LISTENING"
        if ($netstatOutput) {
            # Try to make a request to the API
            try {
                $response = Invoke-RestMethod -Uri "$Url/api/Test/bots" -Method GET -TimeoutSec 5 -ErrorAction SilentlyContinue
                return $true
            }
            catch {
                # Port is in use but API might not be responding properly
                Write-Host "⚠️  Port $TestPort is in use but API not responding properly" -ForegroundColor Yellow
                return $false
            }
        }
        return $false
    }
    catch {
        return $false
    }
}

function Stop-Api {
    param([string]$TestPort)
    
    Write-Host "🛑 Stopping $ApiName..." -ForegroundColor Yellow
    
    # Find and stop dotnet processes running on the port
    $processes = Get-NetTCPConnection -LocalPort $TestPort -ErrorAction SilentlyContinue | 
                 ForEach-Object { Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue } |
                 Where-Object { $_.ProcessName -eq "dotnet" -or $_.ProcessName -eq "FunctionalTests" }
    
    if ($processes) {
        $processes | ForEach-Object {
            Write-Host "  Stopping process: $($_.ProcessName) (PID: $($_.Id))" -ForegroundColor Gray
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        }
        Start-Sleep -Seconds 2
        Write-Host "✅ $ApiName stopped" -ForegroundColor Green
    } else {
        Write-Host "ℹ️  No API processes found running on port $TestPort" -ForegroundColor Gray
    }
}

function Start-Api {
    Write-Host "🚀 Starting $ApiName..." -ForegroundColor Yellow
    
    # Change to project directory
    if (-not (Test-Path $ProjectPath)) {
        Write-Host "❌ Error: $ProjectPath directory not found!" -ForegroundColor Red
        Write-Host "   Make sure you're running this script from the project root." -ForegroundColor Gray
        exit 1
    }
    
    Push-Location $ProjectPath
    
    try {
        # Start the API in background
        Write-Host "  Building project..." -ForegroundColor Gray
        $buildResult = dotnet build --verbosity quiet 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Build failed!" -ForegroundColor Red
            Write-Host $buildResult -ForegroundColor Red
            return $false
        }
        
        Write-Host "  Starting API on $Url..." -ForegroundColor Gray
        Start-Process -WindowStyle Hidden -FilePath "dotnet" -ArgumentList "run", "--urls", $Url
        
        # Wait for API to start
        $maxWait = 30
        $waited = 0
        while ($waited -lt $maxWait) {
            Start-Sleep -Seconds 1
            $waited++
            
            if (Test-ApiRunning -TestPort $Port) {
                Write-Host "✅ $ApiName started successfully!" -ForegroundColor Green
                Write-Host "   Available at: $Url" -ForegroundColor Cyan
                return $true
            }
            
            if ($waited % 5 -eq 0) {
                Write-Host "  Waiting for API to start... ($waited/$maxWait seconds)" -ForegroundColor Gray
            }
        }
        
        Write-Host "❌ API failed to start within $maxWait seconds" -ForegroundColor Red
        return $false
    }
    finally {
        Pop-Location
    }
}

function Show-Status {
    Write-Host "🔍 Checking $ApiName status..." -ForegroundColor Cyan
    
    if (Test-ApiRunning -TestPort $Port) {
        Write-Host "✅ $ApiName is running" -ForegroundColor Green
        Write-Host "   Available at: $Url" -ForegroundColor Cyan
        
        # Try to get API info
        try {
            $response = Invoke-RestMethod -Uri "$Url/api/Test/bots" -Method GET -TimeoutSec 5 -ErrorAction SilentlyContinue
            if ($response) {
                Write-Host "   Available bots: $($response -join ', ')" -ForegroundColor Gray
            }
        }
        catch {
            Write-Host "   API responding but bot list unavailable" -ForegroundColor Gray
        }
        
        return $true
    } else {
        Write-Host "❌ $ApiName is not running" -ForegroundColor Red
        return $false
    }
}

# Main script logic
Write-Host "==================== $ApiName Manager ====================" -ForegroundColor Magenta
Write-Host ""

if ($Stop) {
    Stop-Api -TestPort $Port
    exit 0
}

$isRunning = Show-Status

if ($Force -and $isRunning) {
    Write-Host ""
    Stop-Api -TestPort $Port
    Start-Sleep -Seconds 2
    $isRunning = $false
}

if (-not $isRunning) {
    Write-Host ""
    $started = Start-Api
    
    if (-not $started) {
        Write-Host ""
        Write-Host "❌ Failed to start $ApiName" -ForegroundColor Red
        Write-Host "   Try running manually: cd $ProjectPath && dotnet run --urls $Url" -ForegroundColor Gray
        exit 1
    }
} else {
    Write-Host ""
    Write-Host "ℹ️  $ApiName is already running. Use -Force to restart." -ForegroundColor Gray
}

Write-Host ""
Write-Host "🎯 Quick test command:" -ForegroundColor Yellow
Write-Host "   Invoke-RestMethod -Uri '$Url/api/Test/bots' -Method GET" -ForegroundColor Cyan
Write-Host ""
Write-Host "📊 Open visualizer:" -ForegroundColor Yellow  
Write-Host "   cd visualizer-2d && npm run dev" -ForegroundColor Cyan
Write-Host ""
Write-Host "Usage examples:" -ForegroundColor Yellow
Write-Host "   .\start-api.ps1          # Check status and start if needed" -ForegroundColor Gray
Write-Host "   .\start-api.ps1 -Force   # Force restart" -ForegroundColor Gray
Write-Host "   .\start-api.ps1 -Stop    # Stop the API" -ForegroundColor Gray
Write-Host "============================================================" -ForegroundColor Magenta 