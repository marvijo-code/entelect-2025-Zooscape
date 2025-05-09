@echo off
set COMPOSE_BAKE=true

@REM REM Stop and remove containers if they are running
@REM docker compose down

@REM echo "Building docker images..."
docker compose build --force-recreate

echo "Running engine and bots"
docker compose up -d
docker compose logs --follow o4mcts g2mcts

REM Wait for the 'engine' container to stop
FOR /F "usebackq delims=" %%i IN (`docker compose ps -q engine`) DO docker wait %%i

REM Stop and remove containers
docker compose down