#!/usr/bin/env bash

# Stop and remove containers if they are running
docker compose down

echo "Building docker images..."
docker compose build

echo "Running engine and 3 reference bots"
docker compose up -d
docker compose logs --follow engine

# Wait for the 'engine' container to stop
docker wait $(docker compose ps -q engine)

# Stop and remove containers
docker compose down