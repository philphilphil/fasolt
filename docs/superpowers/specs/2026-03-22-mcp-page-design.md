# MCP Setup Page

## Goal

Give users a dedicated page to understand how the MCP integration works and set it up in their AI client with minimal friction. Focus on Claude Code for now.

## Decisions

- Top-level nav tab (6th: Dashboard, Sources, Cards, Decks, MCP, Settings)
- Route: `/mcp`, authenticated
- Single scrollable page, not a wizard
- Token generation creates a new token each time (named "MCP Server - {timestamp}")
- CLI command uses `window.location.origin` as the API URL
- Primary: `claude mcp add` command. Secondary: collapsible JSON config.
- The flow is user-triggered — the user asks their AI agent to process files, it doesn't happen automatically

## Page Structure

### 1. How It Works

Heading: "How It Works"

Short paragraph (2-3 sentences): "You ask your AI agent to read your local markdown notes and create flashcards. The agent uses the fasolt MCP server to push cards to your account. You study them here."

Below the text, a simple horizontal flow diagram using styled cards/boxes:

```
[Your Notes]  →  [AI Agent + MCP]  →  [fasolt]  →  [Study]
 .md files       You trigger it       Cards stored   Review here
```

Use shadcn Card components or simple styled divs with arrows between them. Keep it minimal — no SVG, no complex diagrams.

### 2. Prerequisites

Heading: "Prerequisites"

Two items as a simple list or small cards:

- **.NET SDK** — "Install the .NET SDK (version 10+)" with a link to https://dot.net/download
- **fasolt MCP tool** — `dotnet tool install --global fasolt-mcp`

Show the dotnet install command in a code block with a copy button.

### 3. Setup

Heading: "Connect to Your AI Client"

**Token generation:**
- A "Generate Access Token" button (primary)
- On click: calls `POST /api/tokens` with name `"MCP Server - {date}"`
- On success: shows the token in a highlighted box with a copy button and a note: "Save this token — you won't be able to see it again."
- Button stays available for generating additional tokens

**Claude Code command (primary):**
- Heading: "Claude Code"
- Pre-filled `claude mcp add` command in a code block with copy button:
  ```
  claude mcp add fasolt -- env FASOLT_URL={origin} FASOLT_TOKEN={token} dotnet tool run fasolt-mcp
  ```
- The `{origin}` is replaced with `window.location.origin`
- The `{token}` is replaced with the generated token value
- If no token generated yet, show placeholder `<your-token>` and disable copy, or prompt to generate first

**Manual JSON config (collapsible):**
- Collapsible section: "Manual configuration"
- Shows the JSON snippet for `.mcp.json` / `~/.claude.json`:
  ```json
  {
    "mcpServers": {
      "fasolt": {
        "command": "dotnet",
        "args": ["tool", "run", "fasolt-mcp"],
        "env": {
          "FASOLT_URL": "{origin}",
          "FASOLT_TOKEN": "{token}"
        }
      }
    }
  }
  ```

## Component Structure

- `McpView.vue` — the page view component, lives in `views/`
- No new store needed — token creation uses existing `createApiToken()` from `api/client.ts`
- No new API endpoints needed — uses existing token API

## State Management

Local component state only (no Pinia store):
- `generatedToken: string | null` — the token value after creation
- `tokenName: string` — auto-generated name
- `loading: boolean` — during token creation
- `error: string | null` — error message if token creation fails

## Navigation Changes

- Add "MCP" tab to `AppLayout.vue` desktop tabs (between Decks and Settings)
- Add "MCP" item to `BottomNav.vue` mobile nav
- Add route to `router/index.ts`

## UI Components Used

All existing shadcn-vue components:
- Card, CardHeader, CardContent for sections
- Button for token generation and copy
- Collapsible for manual config
- Badge or inline code styling for commands
