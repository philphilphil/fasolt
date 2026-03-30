<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { Button } from '@/components/ui/button'
import { Moon, Sun, Bot, Layers, FileText, Brain, BarChart3, Image, Search, Server } from 'lucide-vue-next'
import { useDarkMode } from '@/composables/useDarkMode'
import TerminalDemo from '@/components/TerminalDemo.vue'

const { isDark, toggle } = useDarkMode()
</script>

<template>
  <div class="min-h-screen bg-background text-foreground">
    <!-- Grid background -->
    <div class="pointer-events-none fixed inset-0 -z-10 bg-grid bg-grid-fade opacity-60" />

    <!-- Nav -->
    <nav class="sticky top-0 z-50 border-b border-border/60 bg-background/80 backdrop-blur-md">
      <div class="mx-auto flex max-w-5xl items-center justify-between px-6 py-4">
        <span class="flex items-center gap-2.5 text-sm font-bold tracking-tight">
          <img src="/logo.svg" alt="fasolt" class="h-10 object-contain" />
          fasolt
          <span class="rounded border border-accent/30 bg-accent/10 px-1 text-[8px] leading-4 font-medium text-accent">beta</span>
        </span>
        <div class="flex items-center gap-2">
          <button
            class="rounded p-2 text-muted-foreground hover:text-foreground"
            @click="toggle"
          >
            <Sun v-if="isDark" :size="16" />
            <Moon v-else :size="16" />
          </button>
          <RouterLink to="/login">
            <Button variant="ghost" size="sm" class="text-xs">Log in</Button>
          </RouterLink>
        </div>
      </div>
    </nav>

    <!-- Hero -->
    <section class="relative mx-auto max-w-5xl px-6 pt-20 pb-12 sm:pt-32 sm:pb-16">
      <div class="max-w-2xl">
        <p class="mb-4 text-xs uppercase tracking-[0.2em] text-accent">spaced repetition</p>
        <h1 class="text-2xl sm:text-4xl font-bold tracking-tight leading-tight mb-5">
          <span class="text-accent text-glow">MCP-first</span> spaced repetition<br class="hidden sm:block" />
          for your notes.
        </h1>
        <p class="text-sm text-muted-foreground mb-8 max-w-md leading-relaxed">
          Your AI agent reads your notes and creates flashcards.
          Study them on the web or the iOS app. Free.
        </p>
        <div class="flex flex-wrap gap-3">
          <RouterLink to="/register">
            <Button class="glow-accent">Get started</Button>
          </RouterLink>
          <RouterLink to="/login">
            <Button variant="outline">Log in</Button>
          </RouterLink>
        </div>
      </div>
    </section>

    <!-- Terminal demo -->
    <section class="relative mx-auto max-w-5xl px-6 pb-20">
      <div class="max-w-2xl">
        <TerminalDemo />
      </div>
    </section>

    <!-- How it works -->
    <section class="border-y border-border/60 bg-card/50">
      <div class="mx-auto max-w-5xl px-6 py-16">
        <p class="text-xs uppercase tracking-[0.2em] text-accent mb-8">How it works</p>
        <div class="grid gap-10 sm:grid-cols-3">
          <div>
            <span class="text-xs text-accent/60 mb-2 block">01</span>
            <h3 class="text-sm font-semibold mb-2">Write notes</h3>
            <p class="text-xs text-muted-foreground leading-relaxed">
              Use Obsidian, any editor, or plain text files. No special format required.
            </p>
          </div>
          <div>
            <span class="text-xs text-accent/60 mb-2 block">02</span>
            <h3 class="text-sm font-semibold mb-2">Connect your AI agent</h3>
            <p class="text-xs text-muted-foreground leading-relaxed">
              Add the fasolt MCP server to Claude, Cursor, or any MCP-compatible agent. It reads your notes and creates flashcards.
            </p>
          </div>
          <div>
            <span class="text-xs text-accent/60 mb-2 block">03</span>
            <h3 class="text-sm font-semibold mb-2">Learn and remember</h3>
            <p class="text-xs text-muted-foreground leading-relaxed">
              Study your cards on the web or the iOS app. Spaced repetition schedules reviews at optimal intervals.
            </p>
          </div>
        </div>
      </div>
    </section>

    <!-- Features -->
    <section class="mx-auto max-w-5xl px-6 py-16">
      <p class="text-xs uppercase tracking-[0.2em] text-accent mb-8">Features</p>
      <div class="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
        <div class="rounded border border-border/60 bg-card/50 p-4">
          <Bot :size="16" class="text-accent mb-2" />
          <h3 class="text-sm font-semibold mb-1">MCP tools</h3>
          <p class="text-xs text-muted-foreground leading-relaxed">AI agents create and manage cards via MCP.</p>
        </div>
        <div class="rounded border border-border/60 bg-card/50 p-4">
          <Layers :size="16" class="text-accent mb-2" />
          <h3 class="text-sm font-semibold mb-1">Decks</h3>
          <p class="text-xs text-muted-foreground leading-relaxed">Organize cards into focused study groups.</p>
        </div>
        <div class="rounded border border-border/60 bg-card/50 p-4">
          <FileText :size="16" class="text-accent mb-2" />
          <h3 class="text-sm font-semibold mb-1">Source tracking</h3>
          <p class="text-xs text-muted-foreground leading-relaxed">Cards retain provenance — file and heading.</p>
        </div>
        <div class="rounded border border-border/60 bg-card/50 p-4">
          <Brain :size="16" class="text-accent mb-2" />
          <h3 class="text-sm font-semibold mb-1">FSRS scheduling</h3>
          <p class="text-xs text-muted-foreground leading-relaxed">Optimal review intervals backed by research.</p>
        </div>
        <div class="rounded border border-border/60 bg-card/50 p-4">
          <BarChart3 :size="16" class="text-accent mb-2" />
          <h3 class="text-sm font-semibold mb-1">Dashboard</h3>
          <p class="text-xs text-muted-foreground leading-relaxed">Stats, due counts, and study streaks.</p>
        </div>
        <div class="rounded border border-border/60 bg-card/50 p-4">
          <Image :size="16" class="text-accent mb-2" />
          <h3 class="text-sm font-semibold mb-1">SVG support</h3>
          <p class="text-xs text-muted-foreground leading-relaxed">Diagrams and visualizations on cards.</p>
        </div>
        <div class="rounded border border-border/60 bg-card/50 p-4">
          <Search :size="16" class="text-accent mb-2" />
          <h3 class="text-sm font-semibold mb-1">Search</h3>
          <p class="text-xs text-muted-foreground leading-relaxed">Full-text search across all your cards.</p>
        </div>
        <div class="rounded border border-border/60 bg-card/50 p-4">
          <Server :size="16" class="text-accent mb-2" />
          <h3 class="text-sm font-semibold mb-1">Self-hostable</h3>
          <p class="text-xs text-muted-foreground leading-relaxed">Run your own instance with Docker.</p>
        </div>
      </div>
    </section>

    <!-- iOS showcase -->
    <section class="mx-auto max-w-5xl px-6 py-16">
      <div class="flex flex-col items-center gap-6">
        <p class="text-xs uppercase tracking-[0.2em] text-accent">Study anywhere</p>
        <p class="text-xs text-muted-foreground">Native iOS app — coming soon</p>
        <div class="grid grid-cols-2 gap-4 sm:grid-cols-4">
          <img
            src="/ios_screenshot_dashboard.png"
            alt="iOS app dashboard"
            class="rounded-lg w-full"
          />
          <img
            src="/ios_screenshot_front.png"
            alt="iOS app flashcard front"
            class="rounded-lg w-full"
          />
          <img
            src="/ios_screenshot_back.png"
            alt="iOS app flashcard back"
            class="rounded-lg w-full"
          />
          <img
            src="/ios_screenshot_sessionComplete.png"
            alt="iOS app study session complete"
            class="rounded-lg w-full"
          />
        </div>
      </div>
    </section>

    <!-- CTA -->
    <section class="mx-auto max-w-5xl px-6 py-16 text-center">
      <p class="text-xs text-muted-foreground mb-4 uppercase tracking-[0.2em]">Free and open source</p>
      <RouterLink to="/register">
        <Button class="glow-accent">Get started</Button>
      </RouterLink>
    </section>

    <!-- Footer -->
    <footer class="border-t border-border/40 px-5 py-2 text-[10px] text-muted-foreground/50 flex items-center justify-between">
      <span>fasolt</span>
      <div class="flex items-center gap-3">
        <RouterLink to="/algorithm" class="hover:text-muted-foreground">How the algorithm works</RouterLink>
        <a href="https://github.com/philphilphil/fasolt" target="_blank" rel="noopener noreferrer" class="hover:text-muted-foreground transition-colors" aria-label="GitHub">
          <svg class="size-3.5" viewBox="0 0 24 24" fill="currentColor"><path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0 0 24 12c0-6.63-5.37-12-12-12z"/></svg>
        </a>
        <RouterLink to="/privacy" class="hover:text-muted-foreground">Privacy</RouterLink>
      </div>
    </footer>
  </div>
</template>
