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

# --- PostgreSQL 17 ---
apt-get install -y gnupg lsb-release curl ca-certificates
echo "deb http://apt.postgresql.org/pub/repos/apt $(lsb_release -cs)-pgdg main" \
  > /etc/apt/sources.list.d/pgdg.list
curl -fsSL https://www.postgresql.org/media/keys/ACCC4CF8.asc \
  | gpg --dearmor -o /etc/apt/trusted.gpg.d/postgresql.gpg
apt-get update
apt-get install -y postgresql-17

# Start PostgreSQL and create the dev database
pg_ctlcluster 17 main start
su - postgres -c "psql -c \"CREATE USER fasolt WITH PASSWORD 'fasolt_dev';\"" || true
su - postgres -c "psql -c \"CREATE DATABASE fasolt OWNER fasolt;\"" || true
su - postgres -c "psql -c \"GRANT ALL PRIVILEGES ON DATABASE fasolt TO fasolt;\"" || true
