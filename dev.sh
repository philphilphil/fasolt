#!/bin/bash
set -e
cd "$(dirname "$0")"

# Start Postgres if not running
docker compose up -d

# Run backend and frontend concurrently
dotnet run --project fasolt.Server &
BACKEND_PID=$!

cd fasolt.client && npm run dev &
FRONTEND_PID=$!

trap "kill $BACKEND_PID $FRONTEND_PID 2>/dev/null; exit" INT TERM
wait
