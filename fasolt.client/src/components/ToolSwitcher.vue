<script setup lang="ts">
import { ref, watch, onMounted, onBeforeUnmount, computed } from 'vue'
import { RouterLink } from 'vue-router'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import TerminalDemo from '@/components/TerminalDemo.vue'

type ToolKey = 'chatgpt' | 'claude' | 'other'

const tab = ref<ToolKey>('chatgpt')

const userPrompt = 'Make me flashcards from my LLM basics notes.'
const cards = [
  'What does a tokenizer do and why is it needed?',
  'What is the context window of an LLM?',
  'How does temperature affect text generation?',
  'What is the difference between pre-training and fine-tuning?',
  'Why do LLMs hallucinate?',
  'What is RLHF and what problem does it solve?',
  'Prompt engineering vs fine-tuning — when to use which?',
]
const studyUrl = 'fasolt.app/decks/a1b2c3'

const phase = ref<'thinking' | 'streaming' | 'done'>('thinking')
const visibleCards = ref(0)
const showFooter = ref(false)

const timers: ReturnType<typeof setTimeout>[] = []
const reducedMotion = computed(() =>
  typeof window !== 'undefined' &&
  window.matchMedia &&
  window.matchMedia('(prefers-reduced-motion: reduce)').matches,
)

function clearTimers() {
  while (timers.length) clearTimeout(timers.shift()!)
}

function jumpToDone() {
  clearTimers()
  visibleCards.value = cards.length
  showFooter.value = true
  phase.value = 'done'
}

function runScript() {
  clearTimers()
  visibleCards.value = 0
  showFooter.value = false
  phase.value = 'thinking'

  if (reducedMotion.value) {
    jumpToDone()
    return
  }

  const thinkingMs = 700
  timers.push(setTimeout(() => {
    phase.value = 'streaming'
  }, thinkingMs))

  const cardStagger = 180
  cards.forEach((_, idx) => {
    timers.push(setTimeout(() => {
      visibleCards.value = idx + 1
    }, thinkingMs + 100 + idx * cardStagger))
  })

  const afterCards = thinkingMs + 100 + cards.length * cardStagger
  timers.push(setTimeout(() => {
    showFooter.value = true
    phase.value = 'done'
  }, afterCards + 200))
}

onMounted(runScript)
onBeforeUnmount(clearTimers)
watch(tab, runScript)

const copied = ref(false)
async function copyMcpUrl() {
  try {
    await navigator.clipboard.writeText('https://fasolt.app/mcp')
    copied.value = true
    setTimeout(() => { copied.value = false }, 1500)
  } catch {
    // ignore
  }
}
</script>

<template>
  <div>
    <Tabs v-model="tab" class="w-full">
      <TabsList class="bg-card/60 border border-border/60 rounded-md p-1 gap-1">
        <TabsTrigger value="chatgpt" class="text-xs px-3">ChatGPT</TabsTrigger>
        <TabsTrigger value="claude" class="text-xs px-3">Claude</TabsTrigger>
        <TabsTrigger value="other" class="text-xs px-3 text-muted-foreground/70 data-[state=active]:text-foreground">Other</TabsTrigger>
      </TabsList>

      <!-- ChatGPT -->
      <TabsContent value="chatgpt" class="mt-4">
        <div
          class="flex flex-col overflow-hidden rounded-xl border border-border/60 shadow-2xl glow-accent-lg lg:h-[420px]"
          style="background: #ffffff; font-family: 'Söhne', 'Inter', system-ui, -apple-system, sans-serif;"
        >
          <!-- Window chrome -->
          <div class="flex items-center gap-2 px-4 py-3 border-b" style="border-color: #ececec; background: #f9f9f9;">
            <span class="h-2.5 w-2.5 rounded-full" style="background: #ff5f57"></span>
            <span class="h-2.5 w-2.5 rounded-full" style="background: #febc2e"></span>
            <span class="h-2.5 w-2.5 rounded-full" style="background: #28c840"></span>
            <span class="ml-3 text-[11px]" style="color: #8e8ea0">ChatGPT</span>
          </div>

          <!-- Body -->
          <div class="flex flex-col px-5 py-5 sm:px-6 sm:py-6 text-[13px] sm:text-[14px] flex-1" style="color: #1f2328;">
            <!-- User message -->
            <div class="flex justify-end mb-4">
              <div
                class="rounded-3xl px-4 py-2 max-w-[85%]"
                style="background: #f4f4f4; color: #1f2328;"
              >
                {{ userPrompt }}
              </div>
            </div>

            <!-- Assistant response -->
            <div class="flex gap-3">
              <div
                class="flex-shrink-0 w-7 h-7 rounded-full flex items-center justify-center text-white text-[10px] font-bold mt-0.5"
                style="background: #10a37f;"
              >
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
                  <path d="M12 2v4M12 18v4M4.93 4.93l2.83 2.83M16.24 16.24l2.83 2.83M2 12h4M18 12h4M4.93 19.07l2.83-2.83M16.24 7.76l2.83-2.83"/>
                </svg>
              </div>
              <div class="flex-1 leading-relaxed">
                <div v-if="phase === 'thinking'" class="flex gap-1 items-center pt-1.5" style="color: #8e8ea0">
                  <span class="inline-block w-1.5 h-1.5 rounded-full animate-pulse" style="background: #8e8ea0;"></span>
                  <span class="inline-block w-1.5 h-1.5 rounded-full animate-pulse" style="background: #8e8ea0; animation-delay: 0.15s;"></span>
                  <span class="inline-block w-1.5 h-1.5 rounded-full animate-pulse" style="background: #8e8ea0; animation-delay: 0.3s;"></span>
                </div>
                <template v-else>
                  <p class="mb-3">I'll create flashcards from your notes on LLM basics:</p>
                  <ul class="space-y-1.5 mb-3">
                    <li
                      v-for="(c, i) in cards"
                      :key="i"
                      v-show="i < visibleCards"
                      class="gap-2 transition-opacity duration-300"
                      :class="i < 2 ? 'flex' : (i < 4 ? 'hidden sm:flex' : 'hidden')"
                      :style="{ opacity: i < visibleCards ? 1 : 0 }"
                    >
                      <span style="color: #10a37f;">✓</span>
                      <span>{{ c }}</span>
                    </li>
                  </ul>
                  <p v-if="showFooter" class="text-[12px] sm:text-[13px]" style="color: #6b6c7b;">
                    7 cards created in your “LLM Basics” deck —
                    <a href="#" class="underline" style="color: #10a37f;">{{ studyUrl }}</a>
                  </p>
                </template>
              </div>
            </div>
          </div>
        </div>
      </TabsContent>

      <!-- Claude -->
      <TabsContent value="claude" class="mt-4">
        <div
          class="flex flex-col overflow-hidden rounded-xl border border-border/60 shadow-2xl glow-accent-lg lg:h-[420px]"
          style="background: #faf9f5; font-family: 'Styrene B', 'Inter', system-ui, -apple-system, sans-serif;"
        >
          <!-- Window chrome -->
          <div class="flex items-center gap-2 px-4 py-3 border-b" style="border-color: #e8e6dc; background: #f5f3eb;">
            <span class="h-2.5 w-2.5 rounded-full" style="background: #ff5f57"></span>
            <span class="h-2.5 w-2.5 rounded-full" style="background: #febc2e"></span>
            <span class="h-2.5 w-2.5 rounded-full" style="background: #28c840"></span>
            <span class="ml-3 text-[11px]" style="color: #91918b">Claude</span>
          </div>

          <!-- Body -->
          <div class="flex flex-col px-5 py-5 sm:px-6 sm:py-6 text-[13.5px] sm:text-[14.5px] flex-1" style="color: #2d2c28;">
            <!-- User message -->
            <div class="flex justify-end mb-4">
              <div
                class="rounded-2xl px-4 py-2.5 max-w-[85%]"
                style="background: #efece1; color: #2d2c28;"
              >
                {{ userPrompt }}
              </div>
            </div>

            <!-- Assistant response -->
            <div class="flex gap-3">
              <div
                class="flex-shrink-0 w-7 h-7 rounded-md flex items-center justify-center mt-0.5"
                style="background: #d97757;"
              >
                <svg viewBox="0 0 100 100" width="16" height="16" fill="#ffffff" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
                  <rect x="46" y="6" width="8" height="88" rx="4" />
                  <rect x="6" y="46" width="88" height="8" rx="4" />
                  <rect x="46" y="6" width="8" height="88" rx="4" transform="rotate(45 50 50)" />
                  <rect x="46" y="6" width="8" height="88" rx="4" transform="rotate(-45 50 50)" />
                </svg>
              </div>
              <div class="flex-1 leading-relaxed">
                <div v-if="phase === 'thinking'" class="flex gap-1 items-center pt-1.5" style="color: #91918b">
                  <span class="inline-block w-1.5 h-1.5 rounded-full animate-pulse" style="background: #d97757;"></span>
                  <span class="inline-block w-1.5 h-1.5 rounded-full animate-pulse" style="background: #d97757; animation-delay: 0.15s;"></span>
                  <span class="inline-block w-1.5 h-1.5 rounded-full animate-pulse" style="background: #d97757; animation-delay: 0.3s;"></span>
                </div>
                <template v-else>
                  <p class="mb-3">Here are flashcards from your LLM basics notes:</p>
                  <ul class="space-y-1.5 mb-3">
                    <li
                      v-for="(c, i) in cards"
                      :key="i"
                      v-show="i < visibleCards"
                      class="gap-2 transition-opacity duration-300"
                      :class="i < 2 ? 'flex' : (i < 4 ? 'hidden sm:flex' : 'hidden')"
                      :style="{ opacity: i < visibleCards ? 1 : 0 }"
                    >
                      <span style="color: #d97757;">✓</span>
                      <span>{{ c }}</span>
                    </li>
                  </ul>
                  <p v-if="showFooter" class="text-[12px] sm:text-[13px]" style="color: #6b6a62;">
                    Created 7 cards in your “LLM Basics” deck —
                    <a href="#" class="underline" style="color: #d97757;">{{ studyUrl }}</a>
                  </p>
                </template>
              </div>
            </div>
          </div>
        </div>
      </TabsContent>

      <!-- Other / Developers -->
      <TabsContent value="other" class="mt-4">
        <div class="mb-3 rounded-md border border-border/60 bg-card/60 px-4 py-3 font-mono text-[12px]">
          <div class="mb-1.5 flex items-center gap-2 text-[10px] uppercase tracking-[0.2em] text-accent/80">
            <span class="inline-block h-1.5 w-1.5 rounded-full bg-accent animate-pulse"></span>
            Connect your agent to the MCP
          </div>
          <div class="flex items-center gap-2">
            <span class="text-muted-foreground">$</span>
            <code class="flex-1 select-all break-all text-foreground">https://fasolt.app/mcp</code>
            <button
              class="rounded border border-border/60 px-2 py-0.5 text-[10px] text-muted-foreground hover:text-foreground hover:border-accent/50 transition-colors"
              type="button"
              @click="copyMcpUrl"
            >
              {{ copied ? 'copied' : 'copy' }}
            </button>
          </div>
        </div>
        <TerminalDemo />
        <p class="mt-3 text-xs text-muted-foreground">
          Streamable HTTP transport. Works with Claude Code, Cursor, and any MCP-compatible client.
        </p>
      </TabsContent>
    </Tabs>

    <p class="mt-4 text-xs text-muted-foreground">
      <RouterLink to="/mcp-setup" class="text-accent hover:underline">How to connect →</RouterLink>
    </p>
  </div>
</template>
