@echo off

@REM REM Stop and remove containers if they are running
@REM docker compose down

@REM echo "Building docker images..."
@REM docker compose build

echo "Running engine and 3 reference bots"
docker compose up -d
docker compose logs --follow engine

REM Wait for the 'engine' container to stop
FOR /F "usebackq delims=" %%i IN (`docker compose ps -q engine`) DO docker wait %%i

REM Stop and remove containers
docker compose down