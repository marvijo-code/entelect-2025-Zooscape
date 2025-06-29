@echo off
REM ZooscapeRunner Professional Bot Manager - Launch Script
REM Builds and runs the ZooscapeRunner GUI application

SETLOCAL ENABLEDELAYEDEXPANSION

REM Change to repo root (directory where this script is located)
pushd "%~dp0"

REM Delete old log file to ensure a clean run
if exist debug.txt del debug.txt

echo ======================================
echo 🦁 ZooscapeRunner Professional Launch
echo ======================================
echo.

REM Check if dotnet is available
dotnet --version > nul 2>&1
IF %ERRORLEVEL% NEQ 0 (
    echo ❌ .NET SDK not found. Please install .NET 8.0 SDK
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo 🧹 Cleaning previous builds...
dotnet clean "ZooscapeRunner\ZooscapeRunner.sln" --verbosity quiet >> debug.txt 2>&1

echo 📦 Restoring NuGet packages...
dotnet restore "ZooscapeRunner\ZooscapeRunner.sln" --verbosity quiet >> debug.txt 2>&1
IF %ERRORLEVEL% NEQ 0 (
    echo ❌ NuGet restore failed - see debug.txt for details
    echo.
    echo === Last 10 lines of debug.txt ===
    powershell "Get-Content debug.txt | Select-Object -Last 10"
    pause
    exit /b 1
)

echo 🔨 Building ZooscapeRunner...
dotnet build "ZooscapeRunner\ZooscapeRunner\ZooscapeRunner.csproj" --configuration Release --no-restore --verbosity quiet >> debug.txt 2>&1
IF %ERRORLEVEL% NEQ 0 (
    echo ❌ Build failed - see debug.txt for details
    echo.
    echo === Last 15 lines of debug.txt ===
    powershell "Get-Content debug.txt | Select-Object -Last 15"
    pause
    exit /b 1
)

echo.
echo ✅ Build successful! Launching ZooscapeRunner...
echo.
echo 🚀 Starting Professional Bot Management System...
echo    - Real-time process monitoring
echo    - Automated build and deployment
echo    - Live log streaming
echo    - Intelligent restart capabilities
echo.

REM Launch the application
cd "ZooscapeRunner\ZooscapeRunner"
dotnet run --configuration Release --no-build --framework net8.0-windows10.0.19041.0

REM If we get here, the app exited
echo.
echo 📱 ZooscapeRunner has closed.
pause

popd
endlocal
