<script setup lang="ts">
import { ref, computed } from 'vue'
import { RouterLink } from 'vue-router'
import { Button } from '@/components/ui/button'
import { Copy, Check, ChevronDown } from 'lucide-vue-next'
import { useAuthStore } from '@/stores/auth'
import AppFooter from '@/components/AppFooter.vue'
import FasoltWordmark from '@/components/FasoltWordmark.vue'
import ThemeToggle from '@/components/ThemeToggle.vue'

const auth = useAuthStore()
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
  <div :class="auth.isAuthenticated ? '' : 'flex min-h-screen flex-col bg-background text-foreground'">
    <nav v-if="!auth.isAuthenticated" class="sticky top-0 z-50 border-b border-border/60 bg-background/80 backdrop-blur-md">
      <div class="mx-auto flex max-w-5xl items-center justify-between px-6 py-4">
        <RouterLink to="/" class="flex items-center">
          <FasoltWordmark :size="32" />
        </RouterLink>
        <div class="flex items-center gap-2">
          <ThemeToggle />
          <a href="/login">
            <Button variant="ghost" size="sm" class="text-sm">Log in</Button>
          </a>
          <a href="/register">
            <Button size="sm" class="text-sm">Sign up</Button>
          </a>
        </div>
      </div>
    </nav>

    <main :class="auth.isAuthenticated ? '' : 'mx-auto w-full max-w-5xl flex-1 px-6 py-12'">
      <div :class="auth.isAuthenticated ? 'mcp-page' : 'flex flex-col gap-6'">
        <h1 class="page-title">MCP Setup</h1>

        <p class="text-sm text-muted-foreground leading-relaxed">
          Connect your AI agent to create flashcards from your notes. Copy your MCP URL and add it to your client.
        </p>

        <p class="text-sm text-muted-foreground">
          You'll be asked to log in when your AI client first connects. Pick your client below to see the steps.
        </p>

        <div class="mcp-accordion">
          <details class="mcp-client" open>
            <summary class="mcp-client-head">
              <span class="mcp-client-name">Claude Code</span>
              <ChevronDown class="mcp-client-chevron" :size="16" />
            </summary>
            <div class="mcp-client-body">
              <div class="relative">
                <pre class="rounded border border-border bg-secondary px-3 py-2.5 pr-10 text-sm overflow-x-auto whitespace-pre-wrap break-all">{{ remoteClaudeCommand }}</pre>
                <button
                  class="absolute right-2 top-2 rounded p-1 text-muted-foreground hover:text-foreground transition-colors"
                  @click="copyToClipboard(remoteClaudeCommand, 'claude')"
                >
                  <Check v-if="copiedStates['claude']" class="h-3.5 w-3.5 text-success" />
                  <Copy v-else class="h-3.5 w-3.5" />
                </button>
              </div>
            </div>
          </details>

          <details class="mcp-client">
            <summary class="mcp-client-head">
              <span class="mcp-client-name">Claude.ai Web</span>
              <ChevronDown class="mcp-client-chevron" :size="16" />
            </summary>
            <div class="mcp-client-body">
              <div class="rounded border border-border bg-secondary px-3 py-2.5 text-sm text-muted-foreground">
                <ol class="list-decimal list-inside space-y-1.5">
                  <li>Go to <a href="https://claude.ai/customize/connectors" target="_blank" rel="noopener noreferrer" class="text-foreground underline underline-offset-2">Customize</a> → <span class="font-medium text-foreground">Connectors</span></li>
                  <li>Click <span class="font-medium text-foreground">+</span> then <span class="font-medium text-foreground">Add Custom Connector</span></li>
                  <li>Paste your MCP URL:
                    <span class="inline-flex items-center gap-1.5 mt-1">
                      <code class="rounded border border-border bg-background px-1.5 py-0.5 text-xs">{{ origin }}/mcp</code>
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
          </details>

          <details class="mcp-client">
            <summary class="mcp-client-head">
              <span class="mcp-client-name">Mistral Le Chat</span>
              <ChevronDown class="mcp-client-chevron" :size="16" />
            </summary>
            <div class="mcp-client-body">
              <div class="rounded border border-border bg-secondary px-3 py-2.5 text-sm text-muted-foreground">
                <ol class="list-decimal list-inside space-y-1.5">
                  <li>Open Le Chat → <span class="font-medium text-foreground">Intelligence</span> → <a href="https://chat.mistral.ai/connections" target="_blank" rel="noopener noreferrer" class="text-foreground underline underline-offset-2">Connectors</a></li>
                  <li>Click <span class="font-medium text-foreground">+ Add Connector</span> → <span class="font-medium text-foreground">Custom MCP Connector</span></li>
                  <li>Set <span class="font-medium text-foreground">Connector name</span> to <code class="rounded border border-border bg-background px-1 py-0.5 text-xs">fasolt</code> and paste the server URL:
                    <span class="inline-flex items-center gap-1.5 mt-1">
                      <code class="rounded border border-border bg-background px-1.5 py-0.5 text-xs">{{ origin }}/mcp</code>
                      <button
                        class="rounded p-0.5 text-muted-foreground hover:text-foreground transition-colors"
                        @click="copyToClipboard(`${origin}/mcp`, 'mistral-url')"
                      >
                        <Check v-if="copiedStates['mistral-url']" class="h-3 w-3 text-success" />
                        <Copy v-else class="h-3 w-3" />
                      </button>
                    </span>
                  </li>
                  <li>Click <span class="font-medium text-foreground">Connect</span> and authorize with your fasolt account</li>
                </ol>
                <p class="mt-2"><a href="https://docs.mistral.ai/le-chat/knowledge-integrations/connectors/mcp-connectors/" target="_blank" rel="noopener noreferrer" class="text-foreground underline underline-offset-2">See documentation</a></p>
              </div>
            </div>
          </details>

          <details class="mcp-client">
            <summary class="mcp-client-head">
              <span class="mcp-client-name">ChatGPT</span>
              <ChevronDown class="mcp-client-chevron" :size="16" />
            </summary>
            <div class="mcp-client-body">
              <p class="text-sm text-muted-foreground mb-2">Requires Pro, Team, Enterprise, or Edu plan.</p>
              <div class="rounded border border-border bg-secondary px-3 py-2.5 text-sm text-muted-foreground">
                <ol class="list-decimal list-inside space-y-1.5">
                  <li>Enable <span class="font-medium text-foreground">Developer Mode</span> in <span class="font-medium text-foreground">Settings</span> → <span class="font-medium text-foreground">Apps</span> → <span class="font-medium text-foreground">Advanced Settings</span></li>
                  <li>Click <span class="font-medium text-foreground">Create App</span></li>
                  <li>Paste your MCP URL:
                    <span class="inline-flex items-center gap-1.5 mt-1">
                      <code class="rounded border border-border bg-background px-1.5 py-0.5 text-xs">{{ origin }}/mcp</code>
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
          </details>

          <details class="mcp-client">
            <summary class="mcp-client-head">
              <span class="mcp-client-name">GitHub Copilot</span>
              <ChevronDown class="mcp-client-chevron" :size="16" />
            </summary>
            <div class="mcp-client-body">
              <p class="text-sm text-muted-foreground mb-2">
                Add to <code class="rounded border border-border bg-secondary px-1 py-0.5 text-xs">.vscode/mcp.json</code> or <code class="rounded border border-border bg-secondary px-1 py-0.5 text-xs">~/.copilot/mcp-config.json</code>:
              </p>
              <div class="relative">
                <pre class="rounded border border-border bg-secondary px-3 py-2.5 pr-10 text-sm overflow-x-auto">{{ remoteCopilotConfig }}</pre>
                <button
                  class="absolute right-2 top-2 rounded p-1 text-muted-foreground hover:text-foreground transition-colors"
                  @click="copyToClipboard(remoteCopilotConfig, 'copilot')"
                >
                  <Check v-if="copiedStates['copilot']" class="h-3.5 w-3.5 text-success" />
                  <Copy v-else class="h-3.5 w-3.5" />
                </button>
              </div>
            </div>
          </details>
        </div>
      </div>
    </main>

    <AppFooter v-if="!auth.isAuthenticated" />
  </div>
</template>

<style scoped>
.mcp-page {
  padding: 28px 0 40px;
  display: flex;
  flex-direction: column;
  gap: 22px;
}
.mcp-accordion {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.mcp-client {
  border: 1px solid var(--rule-1);
  border-radius: 10px;
  background: var(--paper-1);
  overflow: hidden;
  transition: border-color .12s;
}
.mcp-client[open] {
  border-color: var(--ink-3);
}
.mcp-client-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  cursor: pointer;
  user-select: none;
  list-style: none;
  font-size: 14px;
  font-weight: 500;
  color: var(--ink-0);
  transition: background .12s;
}
.mcp-client-head::-webkit-details-marker { display: none; }
.mcp-client-head:hover { background: var(--paper-2); }
.mcp-client-name { letter-spacing: -0.005em; }
.mcp-client-chevron {
  color: var(--ink-2);
  transition: transform .15s ease, color .12s;
}
.mcp-client[open] .mcp-client-chevron {
  transform: rotate(180deg);
  color: var(--accent);
}
.mcp-client-body {
  padding: 0 16px 16px;
  border-top: 1px solid var(--rule-1);
  padding-top: 14px;
}
</style>
