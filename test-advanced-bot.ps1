# Test script to run AdvancedMCTSBot with proper environment variables

Write-Host "Testing AdvancedMCTSBot connection..."

# Check if the bot executable exists
$botPath = "Bots\AdvancedMCTSBot\build\Release\AdvancedMCTSBot.exe"
if (-not (Test-Path $botPath)) {
    Write-Host "Bot executable not found. Building..."
    Push-Location "Bots\AdvancedMCTSBot"
    & ".\build.bat"
    Pop-Location
}

# Check if engine is running (using curl which handles the response better)
$curlResult = & curl -s http://localhost:5000/bothub 2>&1
if ($curlResult -like "*Connection ID required*") {
    Write-Host "Engine is running at http://localhost:5000 (SignalR hub responding)"
} else {
    Write-Host "Warning: Could not verify engine status. Proceeding anyway..."
    Write-Host "Response: $curlResult"
}

# Run the bot with environment variables
Write-Host "Starting AdvancedMCTSBot with environment variables..."
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = (Resolve-Path $botPath).Path
$psi.WorkingDirectory = (Resolve-Path "Bots\AdvancedMCTSBot\build").Path
$psi.UseShellExecute = $false
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true

# Set environment variables
$psi.EnvironmentVariables["RUNNER_IPV4"] = "localhost"  # Without http:// prefix
$psi.EnvironmentVariables["RUNNER_PORT"] = "5000"
$psi.EnvironmentVariables["BOT_NICKNAME"] = "AdvancedMCTSBot"
$psi.EnvironmentVariables["Token"] = [System.Guid]::NewGuid().ToString()
$psi.EnvironmentVariables["HUB_NAME"] = "bothub"

Write-Host "Environment variables set:"
Write-Host "  RUNNER_IPV4: localhost"
Write-Host "  RUNNER_PORT: 5000"
Write-Host "  BOT_NICKNAME: AdvancedMCTSBot"
Write-Host "  HUB_NAME: bothub"

$process = [System.Diagnostics.Process]::Start($psi)

# Read output for a few seconds
$timeout = 10
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

Write-Host "`nBot output:"
while (-not $process.HasExited -and $stopwatch.Elapsed.TotalSeconds -lt $timeout) {
    if ($process.StandardOutput.Peek() -ge 0) {
        $line = $process.StandardOutput.ReadLine()
        if ($line) {
            Write-Host $line
        }
    }
    if ($process.StandardError.Peek() -ge 0) {
        $line = $process.StandardError.ReadLine()
        if ($line) {
            Write-Host "ERROR: $line" -ForegroundColor Red
        }
    }
    Start-Sleep -Milliseconds 100
}

if (-not $process.HasExited) {
    Write-Host "`nStopping bot after $timeout seconds..."
    $process.Kill()
}

Write-Host "`nTest completed."