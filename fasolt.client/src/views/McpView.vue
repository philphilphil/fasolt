<script setup lang="ts">
import { ref, computed } from 'vue'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { ArrowRight, Copy, Check } from 'lucide-vue-next'

// Copy button state tracking
const copiedStates = ref<Record<string, boolean>>({})

const origin = computed(() => window.location.origin)

// Remote setup commands
const remoteClaudeCommand = computed(() =>
  `claude mcp add fasolt --transport http ${origin.value}/mcp`
)

const remoteCopilotConfig = computed(() =>
  JSON.stringify({
    mcpServers: {
      fasolt: {
        type: 'http',
        url: `${origin.value}/mcp`,
      },
    },
  }, null, 2)
)

function copyToClipboard(text: string, key: string) {
  navigator.clipboard.writeText(text)
  copiedStates.value[key] = true
  setTimeout(() => {
    copiedStates.value[key] = false
  }, 2000)
}
</script>

<template>
  <div class="flex flex-col gap-6">
    <h1 class="text-lg font-semibold tracking-tight">MCP Setup</h1>

    <!-- Section 1: How It Works -->
    <Card>
      <CardHeader>
        <CardTitle class="text-base">How It Works</CardTitle>
      </CardHeader>
      <CardContent>
        <p class="text-sm text-muted-foreground mb-4">
          You ask your AI agent to read your local markdown notes and create flashcards.
          The agent uses the fasolt MCP server to push cards to your account. You study them here.
        </p>
        <div class="flex flex-wrap items-center justify-center gap-2 sm:gap-3">
          <div class="flex flex-col items-center gap-1 rounded-lg border bg-muted/50 px-4 py-3 text-center min-w-[100px]">
            <span class="text-sm font-medium">Your Notes</span>
            <span class="text-xs text-muted-foreground">.md files</span>
          </div>
          <ArrowRight class="h-4 w-4 shrink-0 text-muted-foreground" />
          <div class="flex flex-col items-center gap-1 rounded-lg border bg-muted/50 px-4 py-3 text-center min-w-[100px]">
            <span class="text-sm font-medium">AI Agent + MCP</span>
            <span class="text-xs text-muted-foreground">You trigger it</span>
          </div>
          <ArrowRight class="h-4 w-4 shrink-0 text-muted-foreground" />
          <div class="flex flex-col items-center gap-1 rounded-lg border bg-muted/50 px-4 py-3 text-center min-w-[100px]">
            <span class="text-sm font-medium">fasolt</span>
            <span class="text-xs text-muted-foreground">Cards stored</span>
          </div>
          <ArrowRight class="h-4 w-4 shrink-0 text-muted-foreground" />
          <div class="flex flex-col items-center gap-1 rounded-lg border bg-muted/50 px-4 py-3 text-center min-w-[100px]">
            <span class="text-sm font-medium">Study</span>
            <span class="text-xs text-muted-foreground">Review here</span>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- Section 2: Add to Your AI Client (remote) -->
    <Card>
      <CardHeader>
        <CardTitle class="text-base">Add to Your AI Client</CardTitle>
      </CardHeader>
      <CardContent>
        <div class="flex flex-col gap-5">
          <p class="text-sm text-muted-foreground">
            You'll be asked to log in when your AI client first connects.
          </p>

          <!-- Claude Code -->
          <div>
            <h3 class="text-sm font-medium mb-2">Claude Code</h3>
            <div class="relative">
              <pre class="rounded-md bg-muted px-3 py-2 pr-10 text-sm font-mono overflow-x-auto whitespace-pre-wrap break-all">{{ remoteClaudeCommand }}</pre>
              <button
                class="absolute right-2 top-2 rounded-md p-1 text-muted-foreground hover:text-foreground hover:bg-muted-foreground/10 transition-colors"
                @click="copyToClipboard(remoteClaudeCommand, 'remote-claude')"
              >
                <Check v-if="copiedStates['remote-claude']" class="h-4 w-4 text-green-600" />
                <Copy v-else class="h-4 w-4" />
              </button>
            </div>
          </div>

          <!-- GitHub Copilot CLI -->
          <div>
            <h3 class="text-sm font-medium mb-2">GitHub Copilot CLI</h3>
            <p class="text-sm text-muted-foreground mb-2">
              Add the following to <code class="rounded bg-muted px-1 py-0.5 text-xs font-mono">~/.copilot/mcp-config.json</code>:
            </p>
            <div class="relative">
              <pre class="rounded-md bg-muted px-3 py-2 pr-10 text-sm font-mono overflow-x-auto">{{ remoteCopilotConfig }}</pre>
              <button
                class="absolute right-2 top-2 rounded-md p-1 text-muted-foreground hover:text-foreground hover:bg-muted-foreground/10 transition-colors"
                @click="copyToClipboard(remoteCopilotConfig, 'remote-copilot')"
              >
                <Check v-if="copiedStates['remote-copilot']" class="h-4 w-4 text-green-600" />
                <Copy v-else class="h-4 w-4" />
              </button>
            </div>
          </div>
        </div>
      </CardContent>
    </Card>

  </div>
</template>
