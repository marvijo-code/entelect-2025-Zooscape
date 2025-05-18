@echo off
setlocal

:: Build the engine service using docker compose
:: The --no-cache flag ensures a clean build

echo Building engine...
docker compose build --no-cache engine

if %ERRORLEVEL% NEQ 0 (
    echo Failed to build engine
    exit /b %ERRORLEVEL%
)

echo Build completed successfully
endlocal