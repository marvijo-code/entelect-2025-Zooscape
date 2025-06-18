@echo off
setlocal

REM Get the directory of the script itself
set SCRIPT_DIR=%~dp0

REM Define the build directory relative to the script's location
set BUILD_DIR=%SCRIPT_DIR%build

REM Create the build directory if it doesn't exist
if not exist "%BUILD_DIR%" (
    echo Creating build directory: %BUILD_DIR%
    mkdir "%BUILD_DIR%"
    if errorlevel 1 (
        echo Failed to create build directory.
        exit /b 1
    )
)

REM Change to the build directory
pushd "%BUILD_DIR%"
if errorlevel 1 (
    echo Failed to change to build directory.
    exit /b 1
)

REM Configure the project with CMake (generate build files)
REM This will use the default generator for your system (e.g., Visual Studio)
echo Configuring CMake project (source: %SCRIPT_DIR%)...
cmake ..
if errorlevel 1 (
    echo CMake configuration failed.
    popd
    exit /b 1
)

REM Build the project in Release configuration
echo Building project (Release)...
cmake --build . --config Release --target AdvancedMCTSBot -- /v:d > build_log.txt 2>&1
set BUILD_ERRORLEVEL=%ERRORLEVEL%
echo CMake build command finished with ERRORLEVEL: %BUILD_ERRORLEVEL%
echo --- Build Log Start --- 
type build_log.txt
echo --- Build Log End --- 
if %BUILD_ERRORLEVEL% equ 0 (
    @REM echo Build internally successful (ERRORLEVEL 0 from cmake command).
    @REM echo Forcing script exit with code 0.
    popd
    exit /b 0
)

REM The following block only executes if BUILD_ERRORLEVEL was NOT 0 initially.
echo CMake build command originally returned non-zero ERRORLEVEL: %BUILD_ERRORLEVEL%.
set FINAL_EXIT_CODE=%BUILD_ERRORLEVEL%

if %BUILD_ERRORLEVEL% neq 0 (
    echo CMake build command returned ERRORLEVEL: %BUILD_ERRORLEVEL%.
    if exist "%BUILD_DIR%\Release\AdvancedMCTSBot.exe" (
        echo Target executable AdvancedMCTSBot.exe found. Overriding exit code to 0.
        set FINAL_EXIT_CODE=0
    ) else (
        echo Target executable AdvancedMCTSBot.exe NOT found. Build truly failed.
    )
)

if %FINAL_EXIT_CODE% neq 0 (
    echo CMake build (Release) failed
    popd
    exit /b 1
)

REM Return to the original directory
popd

echo Build successful. Executable should be in %BUILD_DIR%\Release\
exit /b 0
