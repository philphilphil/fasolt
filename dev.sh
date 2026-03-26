#!/bin/bash
set -e
cd "$(dirname "$0")"

# Start Postgres if not running
docker compose up -d
killall dotnet 2>/dev/null || true

# Run backend (with --watch for hot reload if requested)
if [ "$1" = "--watch" ]; then
  DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1 dotnet watch run --project fasolt.Server &
else
  dotnet run --project fasolt.Server &
fi
BACKEND_PID=$!

cd fasolt.client && npm run dev &
FRONTEND_PID=$!

trap "kill $BACKEND_PID $FRONTEND_PID 2>/dev/null; exit" INT TERM
wait
