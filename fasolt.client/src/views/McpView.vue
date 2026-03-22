<script setup lang="ts">
import { ref, computed } from 'vue'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { ArrowRight, Copy, Check } from 'lucide-vue-next'

const copiedStates = ref<Record<string, boolean>>({})

const origin = computed(() => window.location.origin)

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
    <h1 class="text-base font-semibold tracking-tight">MCP Setup</h1>

    <!-- How It Works -->
    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">How It Works</CardTitle>
      </CardHeader>
      <CardContent>
        <p class="text-xs text-muted-foreground mb-5 leading-relaxed">
          You ask your AI agent to read your local markdown notes and create flashcards.
          The agent uses the fasolt MCP server to push cards to your account. You study them here.
        </p>
        <div class="flex flex-wrap items-center justify-center gap-2 sm:gap-3">
          <div class="flex flex-col items-center gap-1 rounded border border-border/60 bg-muted/30 px-4 py-3 text-center min-w-[100px]">
            <span class="text-xs font-medium">Your Notes</span>
            <span class="text-[10px] text-muted-foreground">.md files</span>
          </div>
          <ArrowRight class="h-3.5 w-3.5 shrink-0 text-accent/50" />
          <div class="flex flex-col items-center gap-1 rounded border border-border/60 bg-muted/30 px-4 py-3 text-center min-w-[100px]">
            <span class="text-xs font-medium">AI Agent + MCP</span>
            <span class="text-[10px] text-muted-foreground">You trigger it</span>
          </div>
          <ArrowRight class="h-3.5 w-3.5 shrink-0 text-accent/50" />
          <div class="flex flex-col items-center gap-1 rounded border border-accent/30 bg-accent/5 px-4 py-3 text-center min-w-[100px]">
            <span class="text-xs font-medium text-accent">fasolt</span>
            <span class="text-[10px] text-muted-foreground">Cards stored</span>
          </div>
          <ArrowRight class="h-3.5 w-3.5 shrink-0 text-accent/50" />
          <div class="flex flex-col items-center gap-1 rounded border border-border/60 bg-muted/30 px-4 py-3 text-center min-w-[100px]">
            <span class="text-xs font-medium">Study</span>
            <span class="text-[10px] text-muted-foreground">Review here</span>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- Add to Your AI Client -->
    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">Add to Your AI Client</CardTitle>
      </CardHeader>
      <CardContent>
        <div class="flex flex-col gap-5">
          <p class="text-xs text-muted-foreground">
            You'll be asked to log in when your AI client first connects.
          </p>

          <!-- Claude Code -->
          <div>
            <h3 class="text-xs font-medium mb-2 text-accent/80">Claude Code</h3>
            <div class="relative">
              <pre class="rounded border border-border/60 bg-muted/30 px-3 py-2.5 pr-10 text-xs overflow-x-auto whitespace-pre-wrap break-all">{{ remoteClaudeCommand }}</pre>
              <button
                class="absolute right-2 top-2 rounded p-1 text-muted-foreground hover:text-accent transition-colors"
                @click="copyToClipboard(remoteClaudeCommand, 'remote-claude')"
              >
                <Check v-if="copiedStates['remote-claude']" class="h-3.5 w-3.5 text-success" />
                <Copy v-else class="h-3.5 w-3.5" />
              </button>
            </div>
          </div>

          <!-- GitHub Copilot CLI -->
          <div>
            <h3 class="text-xs font-medium mb-2 text-accent/80">GitHub Copilot CLI</h3>
            <p class="text-xs text-muted-foreground mb-2">
              Add the following to <code class="rounded border border-border/60 bg-muted/30 px-1 py-0.5 text-[10px]">~/.copilot/mcp-config.json</code>:
            </p>
            <div class="relative">
              <pre class="rounded border border-border/60 bg-muted/30 px-3 py-2.5 pr-10 text-xs overflow-x-auto">{{ remoteCopilotConfig }}</pre>
              <button
                class="absolute right-2 top-2 rounded p-1 text-muted-foreground hover:text-accent transition-colors"
                @click="copyToClipboard(remoteCopilotConfig, 'remote-copilot')"
              >
                <Check v-if="copiedStates['remote-copilot']" class="h-3.5 w-3.5 text-success" />
                <Copy v-else class="h-3.5 w-3.5" />
              </button>
            </div>
          </div>
        </div>
      </CardContent>
    </Card>

  </div>
</template>
