@echo off
REM Restores NuGet packages, builds ZooscapeRunner and launches the Windows head
REM Usage: double-click or run from an elevated command prompt

SETLOCAL ENABLEDELAYEDEXPANSION

REM Change to repo root (directory where this script is located)
pushd "%~dp0"

REM Delete old log file to ensure a clean run
if exist debug.txt del debug.txt

REM Clean the solution
echo === Cleaning solution ===
dotnet clean "ZooscapeRunner\ZooscapeRunner.sln" >> debug.txt 2>&1

REM Restore all projects
echo === Restoring NuGet packages ===
dotnet restore "ZooscapeRunner\ZooscapeRunner.sln" >> debug.txt 2>&1
IF %ERRORLEVEL% NEQ 0 GOTO :error

REM Build the Windows head project (Debug configuration)
echo === Building ZooscapeRunner (Debug) ===
dotnet build "%~dp0\ZooscapeRunner\ZooscapeRunner.Windows\ZooscapeRunner.Windows.csproj" -c Debug >> debug.txt 2>&1
IF %ERRORLEVEL% NEQ 0 GOTO :error

REM Run the Windows head project
echo === Launching ZooscapeRunner.Windows ===
dotnet run --project "ZooscapeRunner\ZooscapeRunner.Windows\ZooscapeRunner.Windows.csproj" -c Debug >> debug.txt 2>&1

:error
echo *** Build failed – see errors above ***
popd
ENDLOCAL
exit /b %ERRORLEVEL%
