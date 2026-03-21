#!/usr/bin/env bash
# Sets up a test user and API token for local MCP server testing.
# Prerequisites: backend running on localhost:5000, Postgres up.
#
# Usage: ./scripts/setup-mcp-test.sh

set -euo pipefail

BASE="http://localhost:5000/api"
EMAIL="mcp-test@test.com"
PASS="Test1234A"
COOKIE_JAR=$(mktemp)

echo "==> Registering test user ($EMAIL)..."
REG_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$BASE/identity/register" \
  -H 'Content-Type: application/json' \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASS\"}")
if [ "$REG_CODE" = "200" ]; then echo "    Created."
elif [ "$REG_CODE" = "400" ]; then echo "    Already exists, skipping."
else echo "    Unexpected: HTTP $REG_CODE"; exit 1; fi

echo "==> Logging in..."
curl -s -c "$COOKIE_JAR" -X POST "$BASE/identity/login?useCookies=true" \
  -H 'Content-Type: application/json' \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASS\"}" -o /dev/null

echo "==> Creating API token..."
RESPONSE=$(curl -s -b "$COOKIE_JAR" -X POST "$BASE/tokens" \
  -H 'Content-Type: application/json' \
  -d '{"name":"Local MCP Test"}')

TOKEN=$(echo "$RESPONSE" | grep -o '"token":"[^"]*"' | cut -d'"' -f4)

if [ -z "$TOKEN" ]; then
  echo "    Failed to create token. Response: $RESPONSE"
  rm -f "$COOKIE_JAR"
  exit 1
fi

rm -f "$COOKIE_JAR"

echo ""
echo "==> Token created: ${TOKEN:0:12}..."
echo ""
echo "To test the MCP server directly:"
echo ""
echo "  SPACED_MD_URL=http://localhost:5000 SPACED_MD_TOKEN=$TOKEN \\"
echo "    dotnet run --project spaced-md.Mcp"
echo ""
echo "To add to Claude Code (~/.claude/settings.json), merge this into mcpServers:"
echo ""
cat <<JSONEOF
  {
    "mcpServers": {
      "spaced-md": {
        "command": "dotnet",
        "args": ["run", "--project", "$(pwd)/spaced-md.Mcp"],
        "env": {
          "SPACED_MD_URL": "http://localhost:5000",
          "SPACED_MD_TOKEN": "$TOKEN"
        }
      }
    }
  }
JSONEOF
echo ""
