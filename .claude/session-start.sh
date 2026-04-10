#!/bin/bash
# Claude Code SessionStart hook — runs every session (new + resumed).
# Handles project-level dependencies and services.
set -e
cd "$(dirname "$0")/.."

# Only run in cloud sessions
if [ "$CLAUDE_CODE_REMOTE" != "true" ]; then
  exit 0
fi

# Ensure .env exists
if [ ! -f .env ]; then
  cp .env.example .env
fi

# Start PostgreSQL if not running
if ! pg_isready -q 2>/dev/null; then
  sudo pg_ctlcluster 17 main start || true
fi

# Restore backend dependencies
dotnet restore --verbosity quiet

# Install frontend dependencies
cd fasolt.client && npm install --silent
