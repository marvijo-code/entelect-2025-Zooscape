@echo off
echo === Advanced MCTS Bot Test Suite ===
echo.

REM Check if CMake is available
echo Checking dependencies...
cmake --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: CMake not found. Please install CMake.
    echo Download from: https://cmake.org/download/
    pause
    exit /b 1
)
echo ✓ CMake found

REM Check if we have a C++ compiler
where cl >nul 2>&1
if %errorlevel% eq 0 (
    echo ✓ MSVC compiler found
    goto :build
)

where g++ >nul 2>&1
if %errorlevel% eq 0 (
    echo ✓ GCC compiler found
    goto :build
)

where clang++ >nul 2>&1
if %errorlevel% eq 0 (
    echo ✓ Clang compiler found
    goto :build
)

echo ERROR: No C++ compiler found. Please install:
echo - Visual Studio Build Tools, or
echo - MinGW-w64, or
echo - Clang
pause
exit /b 1

:build
echo.
echo Building the bot...
call build.bat

if %errorlevel% neq 0 (
    echo Build failed! Please check the error messages above.
    pause
    exit /b 1
)

echo.
echo === Build Complete ===
echo.
echo The Advanced MCTS Bot has been successfully built!
echo.
echo To run against the engine:
echo 1. Start the Zooscape engine
echo 2. Run: cd build ^&^& .\Release\AdvancedMCTSBot.exe
echo.
echo Bot features:
echo ✓ Advanced Monte Carlo Tree Search
echo ✓ Multi-threaded parallel search
echo ✓ 13 sophisticated heuristics
echo ✓ Zookeeper behavior prediction
echo ✓ Power-up optimization
echo ✓ Score streak maximization
echo ✓ Adaptive endgame strategy
echo.
echo This bot is designed to defeat ClingyHeuroBot2 and other competitors!
echo.
pause