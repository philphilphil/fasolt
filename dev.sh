#!/bin/bash
set -e

# Start Postgres if not running
docker compose up -d

# Run backend and frontend concurrently
dotnet run --project spaced-md.Server &
BACKEND_PID=$!

cd spaced-md.client && npm run dev &
FRONTEND_PID=$!

trap "kill $BACKEND_PID $FRONTEND_PID 2>/dev/null; exit" INT TERM
wait
