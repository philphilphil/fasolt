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
    <!-- Nav -->
    <nav class="sticky top-0 z-50 border-b border-border bg-background/80 backdrop-blur-sm">
      <div class="mx-auto flex max-w-5xl items-center justify-between px-6 py-4">
        <span class="flex items-center gap-2 text-lg font-semibold tracking-tight" style="font-family: var(--font-mono)">
          <img src="/logo.png" alt="fasolt" class="h-8 object-contain dark:invert" style="image-rendering: pixelated" />
          fasolt
          <span class="rounded-full border border-border bg-muted px-2 py-0.5 text-[11px] font-medium text-muted-foreground">beta</span>
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
        </div>
      </div>
    </nav>

    <!-- Hero -->
    <section class="mx-auto max-w-5xl px-6 pt-16 pb-10 sm:pt-24 sm:pb-14">
      <div class="max-w-2xl">
        <h1 class="text-3xl sm:text-4xl font-bold tracking-tight leading-tight mb-4">
          MCP-first spaced repetition for your markdown notes.
        </h1>
        <p class="text-base text-muted-foreground mb-8">
          API and browser also fully supported. Free.
        </p>
        <div class="flex flex-wrap gap-3">
          <RouterLink to="/register">
            <Button>Get started</Button>
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
        <h2 class="text-lg font-semibold mb-8">How it works</h2>
        <div class="grid gap-8 sm:grid-cols-3">
          <div>
            <h3 class="text-sm font-semibold mb-1">Write notes</h3>
            <p class="text-sm text-muted-foreground">
              Use Obsidian, any editor, or plain text files. No special format required.
            </p>
          </div>
          <div>
            <h3 class="text-sm font-semibold mb-1">Connect your AI agent</h3>
            <p class="text-sm text-muted-foreground">
              Add the fasolt MCP server to Claude, Cursor, or any MCP-compatible agent. It reads your notes and creates flashcards.
            </p>
          </div>
          <div>
            <h3 class="text-sm font-semibold mb-1">Learn and remember</h3>
            <p class="text-sm text-muted-foreground">
              Study your cards on the web. Spaced repetition schedules reviews automatically.
            </p>
          </div>
        </div>
      </div>
    </section>

    <!-- Study preview -->
    <section class="mx-auto max-w-5xl px-6 py-14">
      <div class="flex flex-col items-center gap-6">
        <p class="text-sm text-muted-foreground">Study on any device</p>
        <div class="flex items-center gap-4">
          <div class="rounded-2xl border border-border bg-muted/30 p-2 shadow-lg">
            <img
              src="/study-question.png"
              alt="Flashcard question side"
              class="rounded-xl w-[180px] sm:w-[200px]"
            />
          </div>
          <div class="rounded-2xl border border-border bg-muted/30 p-2 shadow-lg">
            <img
              src="/study-answer.png"
              alt="Flashcard answer side with rating buttons"
              class="rounded-xl w-[180px] sm:w-[200px]"
            />
          </div>
          <div class="hidden sm:block rounded-2xl border border-border bg-muted/30 p-2 shadow-lg">
            <img
              src="/study-complete.png"
              alt="Study session complete with statistics"
              class="rounded-xl w-[200px]"
            />
          </div>
        </div>
      </div>
    </section>

    <!-- CTA -->
    <section class="mx-auto max-w-5xl px-6 py-14 text-center">
      <p class="text-sm text-muted-foreground mb-4">Free and open source.</p>
      <RouterLink to="/register">
        <Button>Get started</Button>
      </RouterLink>
    </section>

    <!-- Footer -->
    <footer class="border-t border-border">
      <div class="mx-auto max-w-5xl px-6 py-6 flex items-center justify-between">
        <span class="flex items-center gap-2 text-sm text-muted-foreground font-mono">
          <img src="/logo.png" alt="" class="h-5 w-5 object-contain dark:invert" />
          fasolt
        </span>
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
