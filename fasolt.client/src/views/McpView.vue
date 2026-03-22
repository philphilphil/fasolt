<script setup lang="ts">
import { ref, computed } from 'vue'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { createApiToken, isApiError } from '@/api/client'
import { ArrowRight, Copy, Check, ChevronDown, ChevronRight, ExternalLink } from 'lucide-vue-next'

const generatedToken = ref<string | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)
const manualConfigOpen = ref(false)
const advancedOpen = ref(false)

// Copy button state tracking
const copiedStates = ref<Record<string, boolean>>({})

const origin = computed(() => window.location.origin)

const tokenDisplay = computed(() => generatedToken.value || '<your-token>')

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

// Local setup commands
const claudeCodeCommand = computed(() =>
  `claude mcp add fasolt -- env FASOLT_URL=${origin.value} FASOLT_TOKEN=${tokenDisplay.value} fasolt-mcp`
)

const copilotConfig = computed(() =>
  JSON.stringify({
    mcpServers: {
      fasolt: {
        type: 'local',
        command: 'fasolt-mcp',
        args: [],
        env: {
          FASOLT_URL: origin.value,
          FASOLT_TOKEN: tokenDisplay.value,
        },
        tools: ['*'],
      },
    },
  }, null, 2)
)

const manualConfig = computed(() =>
  JSON.stringify({
    mcpServers: {
      fasolt: {
        command: 'fasolt-mcp',
        args: [],
        env: {
          FASOLT_URL: origin.value,
          FASOLT_TOKEN: tokenDisplay.value,
        },
      },
    },
  }, null, 2)
)

async function handleGenerateToken() {
  error.value = null
  loading.value = true
  try {
    const now = new Date()
    const formatted = now.toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' })
    const result = await createApiToken({ name: `MCP Server - ${formatted}` })
    generatedToken.value = result.token
  } catch (e) {
    if (isApiError(e) && e.errors) {
      error.value = Object.values(e.errors).flat().join(' ')
    } else {
      error.value = 'Failed to generate token.'
    }
  } finally {
    loading.value = false
  }
}

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

    <!-- Section 3: Advanced: Local MCP Server -->
    <div>
      <button
        class="flex items-center gap-1.5 text-sm font-medium text-muted-foreground hover:text-foreground transition-colors mb-3"
        @click="advancedOpen = !advancedOpen"
      >
        <ChevronDown v-if="advancedOpen" class="h-4 w-4" />
        <ChevronRight v-else class="h-4 w-4" />
        Advanced: Local MCP Server
      </button>

      <div v-if="advancedOpen" class="flex flex-col gap-6">
        <!-- Prerequisites -->
        <Card>
          <CardHeader>
            <CardTitle class="text-base">Prerequisites</CardTitle>
          </CardHeader>
          <CardContent>
            <div class="flex flex-col gap-4">
              <div>
                <div class="flex items-center gap-2 text-sm font-medium mb-1">
                  <span>1. .NET SDK</span>
                </div>
                <p class="text-sm text-muted-foreground">
                  Install the .NET SDK (version 10+) from
                  <a
                    href="https://dot.net/download"
                    target="_blank"
                    rel="noopener noreferrer"
                    class="inline-flex items-center gap-1 text-primary underline underline-offset-4 hover:text-primary/80"
                  >
                    dot.net/download
                    <ExternalLink class="h-3 w-3" />
                  </a>
                </p>
              </div>
              <div>
                <div class="flex items-center gap-2 text-sm font-medium mb-1">
                  <span>2. fasolt MCP tool</span>
                </div>
                <div class="relative">
                  <pre class="rounded-md bg-muted px-3 py-2 pr-10 text-sm font-mono overflow-x-auto">dotnet tool install --global fasolt-mcp</pre>
                  <button
                    class="absolute right-2 top-2 rounded-md p-1 text-muted-foreground hover:text-foreground hover:bg-muted-foreground/10 transition-colors"
                    @click="copyToClipboard('dotnet tool install --global fasolt-mcp', 'dotnet-install')"
                  >
                    <Check v-if="copiedStates['dotnet-install']" class="h-4 w-4 text-green-600" />
                    <Copy v-else class="h-4 w-4" />
                  </button>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        <!-- Token + Local Setup -->
        <Card>
          <CardHeader>
            <CardTitle class="text-base">Connect to Your AI Client</CardTitle>
          </CardHeader>
          <CardContent>
            <div class="flex flex-col gap-5">
              <!-- Generate Token -->
              <div class="flex flex-col gap-3">
                <Button
                  size="sm"
                  class="self-start"
                  :disabled="loading"
                  @click="handleGenerateToken"
                >
                  {{ loading ? 'Generating...' : 'Generate Access Token' }}
                </Button>

                <div v-if="error" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
                  {{ error }}
                </div>

                <div v-if="generatedToken" class="rounded-md border border-green-500/30 bg-green-500/10 p-3">
                  <div class="flex items-start justify-between gap-2">
                    <code class="block rounded bg-muted px-2 py-1 text-sm font-mono break-all select-all flex-1">{{ generatedToken }}</code>
                    <button
                      class="shrink-0 rounded-md p-1 text-muted-foreground hover:text-foreground hover:bg-muted-foreground/10 transition-colors"
                      @click="copyToClipboard(generatedToken!, 'token')"
                    >
                      <Check v-if="copiedStates['token']" class="h-4 w-4 text-green-600" />
                      <Copy v-else class="h-4 w-4" />
                    </button>
                  </div>
                  <p class="text-xs text-muted-foreground mt-2">Save this token — you won't be able to see it again.</p>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        <!-- Claude Code (local) -->
        <Card>
          <CardHeader>
            <CardTitle class="text-base">Claude Code</CardTitle>
          </CardHeader>
          <CardContent>
            <div class="flex flex-col gap-3">
              <div class="relative">
                <pre class="rounded-md bg-muted px-3 py-2 pr-10 text-sm font-mono overflow-x-auto whitespace-pre-wrap break-all">{{ claudeCodeCommand }}</pre>
                <button
                  class="absolute right-2 top-2 rounded-md p-1 text-muted-foreground hover:text-foreground hover:bg-muted-foreground/10 transition-colors"
                  @click="copyToClipboard(claudeCodeCommand, 'claude-code')"
                >
                  <Check v-if="copiedStates['claude-code']" class="h-4 w-4 text-green-600" />
                  <Copy v-else class="h-4 w-4" />
                </button>
              </div>
              <div>
                <button
                  class="flex items-center gap-1.5 text-sm font-medium text-muted-foreground hover:text-foreground transition-colors self-start"
                  @click="manualConfigOpen = !manualConfigOpen"
                >
                  <ChevronDown v-if="manualConfigOpen" class="h-4 w-4" />
                  <ChevronRight v-else class="h-4 w-4" />
                  Manual configuration
                </button>
                <div v-if="manualConfigOpen" class="relative mt-2">
                  <pre class="rounded-md bg-muted px-3 py-2 pr-10 text-sm font-mono overflow-x-auto">{{ manualConfig }}</pre>
                  <button
                    class="absolute right-2 top-2 rounded-md p-1 text-muted-foreground hover:text-foreground hover:bg-muted-foreground/10 transition-colors"
                    @click="copyToClipboard(manualConfig, 'manual-config')"
                  >
                    <Check v-if="copiedStates['manual-config']" class="h-4 w-4 text-green-600" />
                    <Copy v-else class="h-4 w-4" />
                  </button>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        <!-- GitHub Copilot CLI (local) -->
        <Card>
          <CardHeader>
            <CardTitle class="text-base">GitHub Copilot CLI</CardTitle>
          </CardHeader>
          <CardContent>
            <div class="flex flex-col gap-2">
              <p class="text-sm text-muted-foreground">
                Add the following to <code class="rounded bg-muted px-1 py-0.5 text-xs font-mono">~/.copilot/mcp-config.json</code>:
              </p>
              <div class="relative">
                <pre class="rounded-md bg-muted px-3 py-2 pr-10 text-sm font-mono overflow-x-auto">{{ copilotConfig }}</pre>
                <button
                  class="absolute right-2 top-2 rounded-md p-1 text-muted-foreground hover:text-foreground hover:bg-muted-foreground/10 transition-colors"
                  @click="copyToClipboard(copilotConfig, 'copilot-config')"
                >
                  <Check v-if="copiedStates['copilot-config']" class="h-4 w-4 text-green-600" />
                  <Copy v-else class="h-4 w-4" />
                </button>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  </div>
</template>
