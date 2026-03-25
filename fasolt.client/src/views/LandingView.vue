<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { Button } from '@/components/ui/button'
import { Moon, Sun } from 'lucide-vue-next'
import { useDarkMode } from '@/composables/useDarkMode'
import TerminalDemo from '@/components/TerminalDemo.vue'

const { isDark, toggle } = useDarkMode()
</script>

<template>
  <div class="min-h-screen bg-background text-foreground">
    <!-- Grid background -->
    <div class="pointer-events-none fixed inset-0 bg-grid bg-grid-fade opacity-60" />

    <!-- Nav -->
    <nav class="sticky top-0 z-50 border-b border-border/60 bg-background/80 backdrop-blur-md">
      <div class="mx-auto flex max-w-5xl items-center justify-between px-6 py-4">
        <span class="flex items-center gap-2.5 text-sm font-bold tracking-tight">
          <img src="/logo.svg" alt="fasolt" class="h-7 object-contain" />
          fasolt
          <span class="rounded border border-accent/30 bg-accent/10 px-1.5 py-0.5 text-[10px] font-medium text-accent">beta</span>
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
          for your markdown notes.
        </h1>
        <p class="text-sm text-muted-foreground mb-8 max-w-md leading-relaxed">
          Your AI agent reads your notes and creates flashcards.
          You review them here. API and browser also fully supported. Free.
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
              Study your cards on the web. Spaced repetition schedules reviews at optimal intervals.
            </p>
          </div>
        </div>
      </div>
    </section>

    <!-- Study preview -->
    <section class="mx-auto max-w-5xl px-6 py-16">
      <div class="flex flex-col items-center gap-6">
        <p class="text-xs uppercase tracking-[0.2em] text-muted-foreground">Study on any device</p>
        <div class="flex items-center gap-4">
          <div class="rounded border border-border/60 bg-card p-2 shadow-lg">
            <img
              src="/study-question.png"
              alt="Flashcard question side"
              class="rounded w-[180px] sm:w-[200px]"
            />
          </div>
          <div class="rounded border border-border/60 bg-card p-2 shadow-lg">
            <img
              src="/study-answer.png"
              alt="Flashcard answer side with rating buttons"
              class="rounded w-[180px] sm:w-[200px]"
            />
          </div>
          <div class="hidden sm:block rounded border border-border/60 bg-card p-2 shadow-lg">
            <img
              src="/study-complete.png"
              alt="Study session complete with statistics"
              class="rounded w-[200px]"
            />
          </div>
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
    <footer class="border-t border-border/60">
      <div class="mx-auto max-w-5xl px-6 py-6 flex items-center justify-between">
        <span class="flex items-center gap-2 text-xs text-muted-foreground">
          <img src="/logo.svg" alt="" class="h-4 w-4 object-contain" />
          fasolt
        </span>
        <div class="flex items-center gap-4">
          <RouterLink
            to="/algorithm"
            class="text-xs text-muted-foreground hover:text-accent"
          >
            How the algorithm works
          </RouterLink>
          <a
            href="https://github.com"
            class="text-xs text-muted-foreground hover:text-accent"
          >
            GitHub
          </a>
        </div>
      </div>
    </footer>
  </div>
</template>
