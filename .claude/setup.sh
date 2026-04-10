#!/bin/bash
# Claude Code on the Web — Setup Script
# Paste this into the cloud environment "Setup script" field.
# Runs once as root on Ubuntu 24.04 when a new session starts.
set -e

# --- .NET 10 SDK ---
wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
/tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet
ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet
export DOTNET_ROOT=/usr/share/dotnet

# --- Node.js 22 ---
curl -fsSL https://deb.nodesource.com/setup_22.x | bash -
apt-get install -y nodejs

# --- PostgreSQL (local) ---
# Docker daemon is not available in cloud sessions; use the system PostgreSQL.
PG_VER=$(pg_lsclusters -h 2>/dev/null | awk '{print $1; exit}')
if [ -z "$PG_VER" ]; then
  apt-get install -y postgresql
  PG_VER=$(pg_lsclusters -h | awk '{print $1; exit}')
fi
pg_ctlcluster "$PG_VER" main start || true
# Wait for PostgreSQL to be ready
until pg_isready -q 2>/dev/null; do sleep 1; done
# Create role and database if they don't exist
su - postgres -c "psql -tc \"SELECT 1 FROM pg_roles WHERE rolname='fasolt'\" | grep -q 1 || psql -c \"CREATE USER fasolt WITH PASSWORD 'fasolt_dev';\""
su - postgres -c "psql -tc \"SELECT 1 FROM pg_catalog.pg_database WHERE datname='fasolt'\" | grep -q 1 || psql -c \"CREATE DATABASE fasolt OWNER fasolt;\""
