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
    servers: {
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

    <p class="text-xs text-muted-foreground leading-relaxed">
      Connect your AI agent to create flashcards from your notes. Copy your MCP URL and add it to your client.
    </p>

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
            <h3 class="text-xs font-medium mb-2">Claude.ai Web</h3>
            <div class="rounded border border-border bg-secondary px-3 py-2.5 text-xs text-muted-foreground">
              <ol class="list-decimal list-inside space-y-1.5">
                <li>Go to <a href="https://claude.ai/customize/connectors" target="_blank" rel="noopener noreferrer" class="text-foreground underline underline-offset-2">Customize</a> → <span class="font-medium text-foreground">Connectors</span></li>
                <li>Click <span class="font-medium text-foreground">+</span> then <span class="font-medium text-foreground">Add Custom Connector</span></li>
                <li>Paste your MCP URL:
                  <span class="inline-flex items-center gap-1.5 mt-1">
                    <code class="rounded border border-border bg-background px-1.5 py-0.5 text-[10px]">{{ origin }}/mcp</code>
                    <button
                      class="rounded p-0.5 text-muted-foreground hover:text-foreground transition-colors"
                      @click="copyToClipboard(`${origin}/mcp`, 'claudeai-url')"
                    >
                      <Check v-if="copiedStates['claudeai-url']" class="h-3 w-3 text-success" />
                      <Copy v-else class="h-3 w-3" />
                    </button>
                  </span>
                </li>
                <li>Authorize with your fasolt account</li>
              </ol>
              <p class="mt-2"><a href="https://support.anthropic.com/en/articles/11175166-getting-started-with-custom-connectors-using-remote-mcp" target="_blank" rel="noopener noreferrer" class="text-foreground underline underline-offset-2">See documentation</a></p>
            </div>
          </div>

          <div>
            <h3 class="text-xs font-medium mb-2">ChatGPT</h3>
            <p class="text-xs text-muted-foreground mb-1.5">Requires Pro, Team, Enterprise, or Edu plan.</p>
            <div class="rounded border border-border bg-secondary px-3 py-2.5 text-xs text-muted-foreground">
              <ol class="list-decimal list-inside space-y-1.5">
                <li>Enable <span class="font-medium text-foreground">Developer Mode</span> in <span class="font-medium text-foreground">Settings</span> → <span class="font-medium text-foreground">Apps</span> → <span class="font-medium text-foreground">Advanced Settings</span></li>
                <li>Click <span class="font-medium text-foreground">Create App</span></li>
                <li>Paste your MCP URL:
                  <span class="inline-flex items-center gap-1.5 mt-1">
                    <code class="rounded border border-border bg-background px-1.5 py-0.5 text-[10px]">{{ origin }}/mcp</code>
                    <button
                      class="rounded p-0.5 text-muted-foreground hover:text-foreground transition-colors"
                      @click="copyToClipboard(`${origin}/mcp`, 'chatgpt-url')"
                    >
                      <Check v-if="copiedStates['chatgpt-url']" class="h-3 w-3 text-success" />
                      <Copy v-else class="h-3 w-3" />
                    </button>
                  </span>
                </li>
                <li>Authorize with your fasolt account</li>
              </ol>
              <p class="mt-2"><a href="https://help.openai.com/en/articles/12584461-developer-mode-and-mcp-apps-in-chatgpt-beta" target="_blank" rel="noopener noreferrer" class="text-foreground underline underline-offset-2">See documentation</a></p>
            </div>
          </div>

          <div>
            <h3 class="text-xs font-medium mb-2">GitHub Copilot</h3>
            <p class="text-xs text-muted-foreground mb-2">
              Add to <code class="rounded border border-border bg-secondary px-1 py-0.5 text-[10px]">.vscode/mcp.json</code> or <code class="rounded border border-border bg-secondary px-1 py-0.5 text-[10px]">~/.copilot/mcp-config.json</code>:
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
