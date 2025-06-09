@echo off
echo === Building Advanced MCTS Bot ===

REM Create build directory
if not exist build mkdir build
cd build

REM Configure with CMake
echo Configuring with CMake...
cmake .. -DCMAKE_BUILD_TYPE=Release

REM Check if CMake configuration was successful
if %errorlevel% neq 0 (
    echo CMake configuration failed!
    echo Please ensure you have:
    echo - CMake installed
    echo - Visual Studio Build Tools or MinGW
    echo - libcurl and jsoncpp libraries
    pause
    exit /b 1
)

REM Build the project
echo Building project...
cmake --build . --config Release

REM Check if build was successful
if %errorlevel% eq 0 (
    echo Build successful!
    echo Executable: .\build\Release\AdvancedMCTSBot.exe
    
    REM Copy config file to build directory
    copy ..\config.json .
    
    echo To run the bot:
    echo   cd build
    echo   .\Release\AdvancedMCTSBot.exe
) else (
    echo Build failed!
    pause
    exit /b 1
)

pause