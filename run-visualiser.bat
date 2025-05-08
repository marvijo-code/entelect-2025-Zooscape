@echo off

REM Navigate to the Visualiser project directory
cd /d "%~dp0\Visualiser"

echo "Building the Visualiser project..."
dotnet build visualiser.csproj -c Release

if %ERRORLEVEL% neq 0 (
    echo Build failed. Check the error messages above.
    pause
    exit /b %ERRORLEVEL%
)

echo "Running the Visualiser application..."
dotnet run --project visualiser.csproj --configuration Release

if %ERRORLEVEL% neq 0 (
    echo Run failed. Check the error messages above.
    pause
    exit /b %ERRORLEVEL%
)

echo "Visualiser application has stopped."
pause
