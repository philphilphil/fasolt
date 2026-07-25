<script setup lang="ts">
import { ref, computed } from 'vue'
import { RouterLink } from 'vue-router'
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
    <nav v-if="!auth.isAuthenticated" class="mcp-nav sticky top-0 z-50 border-b">
      <div class="mx-auto flex max-w-5xl items-center justify-between px-6 py-4">
        <RouterLink to="/" class="flex items-center">
          <FasoltWordmark :size="32" />
        </RouterLink>
        <div class="flex items-center gap-2">
          <RouterLink to="/library" class="fa-btn fa-btn-ghost">Library</RouterLink>
          <ThemeToggle />
          <a href="/login" class="fa-btn fa-btn-ghost">Log in</a>
          <a href="/register" class="fa-btn fa-btn-primary">Sign up</a>
        </div>
      </div>
    </nav>

    <main :class="auth.isAuthenticated ? '' : 'mx-auto w-full max-w-5xl flex-1 px-6 py-12'">
      <div :class="auth.isAuthenticated ? 'mcp-page' : 'flex flex-col gap-6'">
        <h1 class="page-title">MCP setup</h1>

        <p class="mcp-intro">
          Connect your AI agent to create flashcards from your notes. Copy your MCP URL and add it to your client.
        </p>

        <p class="mcp-intro">
          You'll be asked to log in when your AI client first connects. Pick your client below to see the steps.
        </p>

        <div class="mcp-accordion">
          <details class="mcp-client" open>
            <summary class="mcp-client-head">
              <span class="mcp-client-name">Claude Code</span>
              <ChevronDown class="mcp-client-chevron" :size="16" />
            </summary>
            <div class="mcp-client-body">
              <div class="mcp-code">
                <pre class="mcp-pre fa-mono whitespace-pre-wrap break-all">{{ remoteClaudeCommand }}</pre>
                <button
                  class="mcp-copy"
                  aria-label="Copy command"
                  @click="copyToClipboard(remoteClaudeCommand, 'claude')"
                >
                  <Check v-if="copiedStates['claude']" class="h-3.5 w-3.5" :style="{ color: 'var(--c-good)' }" />
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
              <div class="fa-prose mcp-steps">
                <ol>
                  <li>Go to <a href="https://claude.ai/customize/connectors" target="_blank" rel="noopener noreferrer">Customize</a> → <strong>Connectors</strong></li>
                  <li>Click <strong>+</strong> then <strong>Add Custom Connector</strong></li>
                  <li>Paste your MCP URL:
                    <span class="mcp-inline-url">
                      <code class="fa-mono">{{ origin }}/mcp</code>
                      <button
                        class="mcp-copy mcp-copy-sm"
                        aria-label="Copy URL"
                        @click="copyToClipboard(`${origin}/mcp`, 'claudeai-url')"
                      >
                        <Check v-if="copiedStates['claudeai-url']" class="h-3 w-3" :style="{ color: 'var(--c-good)' }" />
                        <Copy v-else class="h-3 w-3" />
                      </button>
                    </span>
                  </li>
                  <li>Authorize with your fasolt account</li>
                </ol>
                <p><a href="https://support.anthropic.com/en/articles/11175166-getting-started-with-custom-connectors-using-remote-mcp" target="_blank" rel="noopener noreferrer">See documentation</a></p>
              </div>
            </div>
          </details>

          <details class="mcp-client">
            <summary class="mcp-client-head">
              <span class="mcp-client-name">Mistral Le Chat</span>
              <ChevronDown class="mcp-client-chevron" :size="16" />
            </summary>
            <div class="mcp-client-body">
              <div class="fa-prose mcp-steps">
                <ol>
                  <li>Open Le Chat → <strong>Intelligence</strong> → <a href="https://chat.mistral.ai/connections" target="_blank" rel="noopener noreferrer">Connectors</a></li>
                  <li>Click <strong>+ Add Connector</strong> → <strong>Custom MCP Connector</strong></li>
                  <li>Set <strong>Connector name</strong> to <code class="fa-mono">fasolt</code> and paste the server URL:
                    <span class="mcp-inline-url">
                      <code class="fa-mono">{{ origin }}/mcp</code>
                      <button
                        class="mcp-copy mcp-copy-sm"
                        aria-label="Copy URL"
                        @click="copyToClipboard(`${origin}/mcp`, 'mistral-url')"
                      >
                        <Check v-if="copiedStates['mistral-url']" class="h-3 w-3" :style="{ color: 'var(--c-good)' }" />
                        <Copy v-else class="h-3 w-3" />
                      </button>
                    </span>
                  </li>
                  <li>Click <strong>Connect</strong> and authorize with your fasolt account</li>
                </ol>
                <p><a href="https://docs.mistral.ai/le-chat/knowledge-integrations/connectors/mcp-connectors/" target="_blank" rel="noopener noreferrer">See documentation</a></p>
              </div>
            </div>
          </details>

          <details class="mcp-client">
            <summary class="mcp-client-head">
              <span class="mcp-client-name">ChatGPT</span>
              <ChevronDown class="mcp-client-chevron" :size="16" />
            </summary>
            <div class="mcp-client-body">
              <p class="mcp-note">Requires Pro, Team, Enterprise, or Edu plan.</p>
              <div class="fa-prose mcp-steps">
                <ol>
                  <li>Enable <strong>Developer Mode</strong> in <strong>Settings</strong> → <strong>Apps</strong> → <strong>Advanced Settings</strong></li>
                  <li>Click <strong>Create App</strong></li>
                  <li>Paste your MCP URL:
                    <span class="mcp-inline-url">
                      <code class="fa-mono">{{ origin }}/mcp</code>
                      <button
                        class="mcp-copy mcp-copy-sm"
                        aria-label="Copy URL"
                        @click="copyToClipboard(`${origin}/mcp`, 'chatgpt-url')"
                      >
                        <Check v-if="copiedStates['chatgpt-url']" class="h-3 w-3" :style="{ color: 'var(--c-good)' }" />
                        <Copy v-else class="h-3 w-3" />
                      </button>
                    </span>
                  </li>
                  <li>Authorize with your fasolt account</li>
                </ol>
                <p><a href="https://help.openai.com/en/articles/12584461-developer-mode-and-mcp-apps-in-chatgpt-beta" target="_blank" rel="noopener noreferrer">See documentation</a></p>
              </div>
            </div>
          </details>

          <details class="mcp-client">
            <summary class="mcp-client-head">
              <span class="mcp-client-name">GitHub Copilot</span>
              <ChevronDown class="mcp-client-chevron" :size="16" />
            </summary>
            <div class="mcp-client-body">
              <p class="mcp-note">
                Add to <code class="fa-mono mcp-inline-code">.vscode/mcp.json</code> or <code class="fa-mono mcp-inline-code">~/.copilot/mcp-config.json</code>:
              </p>
              <div class="mcp-code">
                <pre class="mcp-pre fa-mono">{{ remoteCopilotConfig }}</pre>
                <button
                  class="mcp-copy"
                  aria-label="Copy config"
                  @click="copyToClipboard(remoteCopilotConfig, 'copilot')"
                >
                  <Check v-if="copiedStates['copilot']" class="h-3.5 w-3.5" :style="{ color: 'var(--c-good)' }" />
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
.mcp-nav {
  background: var(--paper-0);
}
.mcp-page {
  padding: 28px 0 40px;
  display: flex;
  flex-direction: column;
  gap: 18px;
}
.mcp-intro {
  font-size: 14px;
  line-height: 1.55;
  color: var(--ink-1);
  margin: 0;
  max-width: 60ch;
}

/* Client list — rows separated by hairlines, no boxed cards */
.mcp-accordion {
  display: flex;
  flex-direction: column;
  border-top: 1px solid var(--rule-1);
}
.mcp-client {
  border-bottom: 1px solid var(--rule-1);
}
.mcp-client-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 4px;
  cursor: pointer;
  user-select: none;
  list-style: none;
  font-size: 15px;
  font-weight: 500;
  color: var(--ink-0);
  transition: color .12s;
}
.mcp-client-head::-webkit-details-marker { display: none; }
.mcp-client-head:hover .mcp-client-name { color: var(--accent-text); }
.mcp-client-name { letter-spacing: -0.01em; transition: color .12s; }
.mcp-client-chevron {
  color: var(--ink-3);
  transition: transform .15s ease, color .12s;
}
.mcp-client[open] .mcp-client-chevron {
  transform: rotate(180deg);
  color: var(--ink-2);
}
.mcp-client-body {
  padding: 2px 4px 18px;
}

/* Step lists (fa-prose handles type; trim trailing margins) */
.mcp-steps { font-size: 14px; }
.mcp-steps ol { margin: 0; }
.mcp-steps li { margin: .3em 0; }
.mcp-steps p { margin: .8em 0 0; }
.mcp-note {
  font-size: 13px;
  color: var(--ink-2);
  margin: 0 0 10px;
}

/* Inline code chip */
.mcp-inline-code,
.mcp-inline-url code {
  font-size: 12.5px;
  padding: 1px 6px;
  background: var(--paper-2);
  border: 1px solid var(--rule-1);
  border-radius: 5px;
  color: var(--ink-0);
}
.mcp-inline-url {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  margin-top: 6px;
}

/* Code block — quiet sunken surface, system mono, flat copy button */
.mcp-code {
  position: relative;
  margin-top: 4px;
}
.mcp-pre {
  margin: 0;
  padding: 12px 40px 12px 14px;
  font-size: 13px;
  line-height: 1.5;
  color: var(--ink-0);
  background: var(--paper-2);
  border: 1px solid var(--rule-1);
  border-radius: 9px;
  overflow-x: auto;
}
.mcp-copy {
  position: absolute;
  top: 8px;
  right: 8px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 5px;
  border: none;
  border-radius: 6px;
  background: transparent;
  color: var(--ink-2);
  cursor: pointer;
  transition: color .12s, background .12s;
}
.mcp-copy:hover { color: var(--ink-0); background: var(--paper-1); }
.mcp-copy-sm {
  position: static;
  padding: 3px;
}
</style>
