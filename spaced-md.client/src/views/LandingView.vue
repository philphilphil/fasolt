<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { Button } from '@/components/ui/button'
import { ArrowRight, Moon, Sun, BookOpen, Cpu, Repeat2 } from 'lucide-vue-next'
import { useDarkMode } from '@/composables/useDarkMode'
import TerminalDemo from '@/components/TerminalDemo.vue'

const { isDark, toggle } = useDarkMode()
</script>

<template>
  <div class="min-h-screen bg-background text-foreground">
    <!-- Nav -->
    <nav class="sticky top-0 z-50 border-b border-border bg-background/80 backdrop-blur-sm">
      <div class="mx-auto flex max-w-5xl items-center justify-between px-6 py-4">
        <span class="text-lg font-semibold tracking-tight" style="font-family: var(--font-mono)">
          spaced-md
        </span>
        <div class="flex items-center gap-2">
          <button
            class="rounded-md p-2 text-muted-foreground hover:text-foreground transition-colors"
            @click="toggle"
          >
            <Sun v-if="isDark" :size="18" />
            <Moon v-else :size="18" />
          </button>
          <RouterLink to="/login">
            <Button variant="ghost" size="sm">Log in</Button>
          </RouterLink>
          <RouterLink to="/register">
            <Button size="sm">Sign up</Button>
          </RouterLink>
        </div>
      </div>
    </nav>

    <!-- Hero -->
    <section class="mx-auto max-w-5xl px-6 pt-16 pb-10 sm:pt-24 sm:pb-14">
      <div class="max-w-2xl">
        <div
          class="mb-4 inline-flex items-center gap-2 rounded-full border border-border bg-muted/50 px-3 py-1 text-xs text-muted-foreground"
          style="font-family: var(--font-mono)"
        >
          <span
            class="h-1.5 w-1.5 rounded-full"
            style="background: hsl(var(--accent))"
          ></span>
          MCP-first spaced repetition
        </div>
        <h1 class="text-4xl sm:text-5xl font-bold tracking-tight leading-tight mb-5">
          Your AI reads your notes.<br />
          <span style="color: hsl(var(--accent))">You just review.</span>
        </h1>
        <p class="text-base text-muted-foreground leading-relaxed mb-8 max-w-xl">
          Connect spaced-md to Claude or any MCP-compatible agent. Point it at your Obsidian vault.
          Flashcards appear automatically. Come back to review when they're due.
        </p>
        <div class="flex flex-wrap gap-3">
          <RouterLink to="/register">
            <Button class="gap-2">
              Get started free
              <ArrowRight :size="16" />
            </Button>
          </RouterLink>
          <RouterLink to="/login">
            <Button variant="outline">Log in</Button>
          </RouterLink>
        </div>
      </div>
    </section>

    <!-- Terminal demo -->
    <section class="mx-auto max-w-5xl px-6 pb-16">
      <div class="max-w-2xl">
        <TerminalDemo />
      </div>
    </section>

    <!-- How it works -->
    <section class="border-y border-border bg-muted/30">
      <div class="mx-auto max-w-5xl px-6 py-14">
        <h2 class="text-xl font-semibold mb-1">How it works</h2>
        <p class="text-sm text-muted-foreground mb-10">Three steps. Your notes do most of the work.</p>
        <div class="grid gap-8 sm:grid-cols-3">
          <!-- Step 1 -->
          <div class="flex flex-col gap-3">
            <div class="flex items-center gap-3">
              <div
                class="flex h-8 w-8 shrink-0 items-center justify-center rounded-md"
                style="background: hsl(var(--muted)); border: 1px solid hsl(var(--border))"
              >
                <BookOpen :size="15" class="text-muted-foreground" />
              </div>
              <span
                class="text-xs font-medium uppercase tracking-widest text-muted-foreground"
                style="font-family: var(--font-mono)"
              >Step 1</span>
            </div>
            <h3 class="text-sm font-semibold">Take notes in Obsidian</h3>
            <p class="text-xs text-muted-foreground leading-relaxed">
              Write in your normal workflow. No special syntax required — or add
              <code class="rounded bg-muted px-1 py-0.5 text-[11px] font-mono">?::</code> markers
              for precise card boundaries.
            </p>
          </div>

          <!-- Step 2 -->
          <div class="flex flex-col gap-3">
            <div class="flex items-center gap-3">
              <div
                class="flex h-8 w-8 shrink-0 items-center justify-center rounded-md"
                style="background: hsl(var(--muted)); border: 1px solid hsl(var(--border))"
              >
                <Cpu :size="15" class="text-muted-foreground" />
              </div>
              <span
                class="text-xs font-medium uppercase tracking-widest text-muted-foreground"
                style="font-family: var(--font-mono)"
              >Step 2</span>
            </div>
            <h3 class="text-sm font-semibold">Your AI creates flashcards</h3>
            <p class="text-xs text-muted-foreground leading-relaxed">
              Ask Claude (or any MCP agent) to read a file and push cards to spaced-md. It handles
              deduplication, deck assignment, and provenance tracking.
            </p>
          </div>

          <!-- Step 3 -->
          <div class="flex flex-col gap-3">
            <div class="flex items-center gap-3">
              <div
                class="flex h-8 w-8 shrink-0 items-center justify-center rounded-md"
                style="background: hsl(var(--muted)); border: 1px solid hsl(var(--border))"
              >
                <Repeat2 :size="15" class="text-muted-foreground" />
              </div>
              <span
                class="text-xs font-medium uppercase tracking-widest text-muted-foreground"
                style="font-family: var(--font-mono)"
              >Step 3</span>
            </div>
            <h3 class="text-sm font-semibold">Review in spaced-md</h3>
            <p class="text-xs text-muted-foreground leading-relaxed">
              Open the web app when cards are due. SM-2 schedules reviews at increasing intervals —
              harder cards come back sooner, mastered cards fade into the background.
            </p>
          </div>
        </div>
      </div>
    </section>

    <!-- Details / under the hood -->
    <section class="mx-auto max-w-5xl px-6 py-14">
      <h2 class="text-xl font-semibold mb-1">Built for developers</h2>
      <p class="text-sm text-muted-foreground mb-10">
        Works with any MCP-compatible client. No proprietary integrations.
      </p>
      <div class="grid gap-4 sm:grid-cols-2">
        <div class="rounded-lg border border-border bg-card p-5">
          <h3 class="text-sm font-semibold mb-1.5">MCP server included</h3>
          <p class="text-xs text-muted-foreground leading-relaxed mb-3">
            Connects Claude Desktop, Cursor, or any MCP host to your spaced-md account via a single
            token.
          </p>
          <pre
            class="rounded bg-muted px-3 py-2 text-[11px] overflow-x-auto"
            style="font-family: var(--font-mono); color: hsl(var(--muted-foreground))"
          >SPACED_MD_TOKEN=&lt;token&gt; \
  dotnet run --project spaced-md.Mcp</pre>
        </div>
        <div class="rounded-lg border border-border bg-card p-5">
          <h3 class="text-sm font-semibold mb-1.5">REST API for everything else</h3>
          <p class="text-xs text-muted-foreground leading-relaxed mb-3">
            Every action in the UI is also an API call. Build your own integrations, scripts, or
            custom agents.
          </p>
          <pre
            class="rounded bg-muted px-3 py-2 text-[11px] overflow-x-auto"
            style="font-family: var(--font-mono); color: hsl(var(--muted-foreground))"
          >POST /api/cards/bulk
Authorization: Bearer &lt;token&gt;</pre>
        </div>
        <div class="rounded-lg border border-border bg-card p-5">
          <h3 class="text-sm font-semibold mb-1.5">Source provenance</h3>
          <p class="text-xs text-muted-foreground leading-relaxed">
            Cards remember which file and heading they came from. Browse by source, see which notes
            have cards, and re-sync when notes change.
          </p>
        </div>
        <div class="rounded-lg border border-border bg-card p-5">
          <h3 class="text-sm font-semibold mb-1.5">Duplicate detection</h3>
          <p class="text-xs text-muted-foreground leading-relaxed">
            Bulk card creation automatically skips cards that already exist. Safe to re-run on the
            same file as your notes evolve.
          </p>
        </div>
      </div>
    </section>

    <!-- CTA -->
    <section class="border-t border-border bg-muted/30">
      <div class="mx-auto max-w-5xl px-6 py-14 text-center">
        <h2 class="text-2xl font-semibold mb-2">Start reviewing what you actually wrote</h2>
        <p class="text-sm text-muted-foreground mb-6">
          Free and open source. Connect your vault in minutes.
        </p>
        <div class="flex flex-wrap items-center justify-center gap-3">
          <RouterLink to="/register">
            <Button class="gap-2">
              Create an account
              <ArrowRight :size="16" />
            </Button>
          </RouterLink>
          <RouterLink to="/login">
            <Button variant="outline">Log in</Button>
          </RouterLink>
        </div>
      </div>
    </section>

    <!-- Footer -->
    <footer class="border-t border-border">
      <div class="mx-auto max-w-5xl px-6 py-6 flex items-center justify-between">
        <span class="text-sm text-muted-foreground font-mono">spaced-md</span>
        <a
          href="https://github.com"
          class="text-sm text-muted-foreground hover:text-foreground transition-colors"
        >
          GitHub
        </a>
      </div>
    </footer>
  </div>
</template>
