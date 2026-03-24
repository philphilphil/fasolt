<script setup lang="ts">
import { ref, computed } from 'vue'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Copy, Check } from 'lucide-vue-next'

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
  setTimeout(() => { copiedStates.value[key] = false }, 2000)
}
</script>

<template>
  <div class="flex flex-col gap-6">
    <h1 class="text-lg font-bold tracking-tight">MCP Setup</h1>

    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">How It Works</CardTitle>
      </CardHeader>
      <CardContent>
        <p class="text-xs text-muted-foreground leading-relaxed">
          You ask your AI agent to read your local markdown notes and create flashcards.
          The agent uses the fasolt MCP server to push cards to your account. You study them here.
        </p>
      </CardContent>
    </Card>

    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">Add to Your AI Client</CardTitle>
      </CardHeader>
      <CardContent>
        <div class="flex flex-col gap-5">
          <p class="text-xs text-muted-foreground">
            You'll be asked to log in when your AI client first connects.
          </p>

          <div>
            <h3 class="text-xs font-medium mb-2">Claude Code</h3>
            <div class="relative">
              <pre class="rounded border border-border bg-secondary px-3 py-2.5 pr-10 text-xs overflow-x-auto whitespace-pre-wrap break-all">{{ remoteClaudeCommand }}</pre>
              <button
                class="absolute right-2 top-2 rounded p-1 text-muted-foreground hover:text-foreground transition-colors"
                @click="copyToClipboard(remoteClaudeCommand, 'claude')"
              >
                <Check v-if="copiedStates['claude']" class="h-3.5 w-3.5 text-success" />
                <Copy v-else class="h-3.5 w-3.5" />
              </button>
            </div>
          </div>

          <div>
            <h3 class="text-xs font-medium mb-2">GitHub Copilot CLI</h3>
            <p class="text-xs text-muted-foreground mb-2">
              Add to <code class="rounded border border-border bg-secondary px-1 py-0.5 text-[10px]">~/.copilot/mcp-config.json</code>:
            </p>
            <div class="relative">
              <pre class="rounded border border-border bg-secondary px-3 py-2.5 pr-10 text-xs overflow-x-auto">{{ remoteCopilotConfig }}</pre>
              <button
                class="absolute right-2 top-2 rounded p-1 text-muted-foreground hover:text-foreground transition-colors"
                @click="copyToClipboard(remoteCopilotConfig, 'copilot')"
              >
                <Check v-if="copiedStates['copilot']" class="h-3.5 w-3.5 text-success" />
                <Copy v-else class="h-3.5 w-3.5" />
              </button>
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  </div>
</template>
