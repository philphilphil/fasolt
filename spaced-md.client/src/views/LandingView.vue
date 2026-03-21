<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { Button } from '@/components/ui/button'
import {
  FileText,
  Brain,
  Upload,
  Layers,
  BarChart3,
  FolderOpen,
  ArrowRight,
  Moon,
  Sun,
} from 'lucide-vue-next'
import { useDarkMode } from '@/composables/useDarkMode'

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
    <section class="mx-auto max-w-5xl px-6 pt-14 pb-12 sm:pt-20 sm:pb-16">
      <div class="max-w-2xl">
        <h1 class="text-4xl sm:text-5xl font-bold tracking-tight leading-tight mb-4">
          Turn your markdown into
          <span style="color: hsl(var(--accent))">lasting knowledge</span>
        </h1>
        <p class="text-lg text-muted-foreground leading-relaxed mb-6">
          Upload <code class="rounded bg-muted px-1.5 py-0.5 text-sm font-mono">.md</code>
          files, create flashcards from them, and review with SM-2 spaced repetition.
        </p>
        <div class="flex gap-3">
          <RouterLink to="/register">
            <Button class="gap-2">
              Get started
              <ArrowRight :size="16" />
            </Button>
          </RouterLink>
          <RouterLink to="/login">
            <Button variant="outline">Log in</Button>
          </RouterLink>
        </div>
      </div>
    </section>

    <!-- How it works -->
    <section class="border-y border-border bg-muted/30">
      <div class="mx-auto max-w-5xl px-6 py-12">
        <h2 class="text-2xl font-semibold mb-6">How it works</h2>
        <div class="grid gap-6 sm:grid-cols-3">
          <div class="flex gap-3">
            <div
              class="flex h-7 w-7 shrink-0 items-center justify-center rounded text-xs font-bold"
              style="background: hsl(var(--accent)); color: hsl(var(--accent-foreground))"
            >1</div>
            <div>
              <h3 class="text-sm font-medium">Upload notes</h3>
              <p class="text-xs text-muted-foreground mt-0.5">Drop in markdown files. Headings are parsed automatically.</p>
            </div>
          </div>
          <div class="flex gap-3">
            <div
              class="flex h-7 w-7 shrink-0 items-center justify-center rounded text-xs font-bold"
              style="background: hsl(var(--accent)); color: hsl(var(--accent-foreground))"
            >2</div>
            <div>
              <h3 class="text-sm font-medium">Create cards</h3>
              <p class="text-xs text-muted-foreground mt-0.5">From an entire file or a specific section.</p>
            </div>
          </div>
          <div class="flex gap-3">
            <div
              class="flex h-7 w-7 shrink-0 items-center justify-center rounded text-xs font-bold"
              style="background: hsl(var(--accent)); color: hsl(var(--accent-foreground))"
            >3</div>
            <div>
              <h3 class="text-sm font-medium">Review & remember</h3>
              <p class="text-xs text-muted-foreground mt-0.5">SM-2 schedules reviews at increasing intervals. Harder cards come back sooner.</p>
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- Features -->
    <section class="mx-auto max-w-5xl px-6 py-12">
      <h2 class="text-2xl font-semibold mb-6">Features</h2>
      <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <div class="rounded-lg border border-border bg-card p-4">
          <div class="flex items-center gap-2 mb-1">
            <Upload :size="16" class="text-muted-foreground" />
            <h3 class="text-sm font-medium">Markdown upload</h3>
          </div>
          <p class="text-xs text-muted-foreground">Drag and drop <code class="text-[11px] font-mono">.md</code> files. Headings are parsed for section-level card creation.</p>
        </div>
        <div class="rounded-lg border border-border bg-card p-4">
          <div class="flex items-center gap-2 mb-1">
            <Layers :size="16" class="text-muted-foreground" />
            <h3 class="text-sm font-medium">Section-level cards</h3>
          </div>
          <p class="text-xs text-muted-foreground">Pick a heading and create cards for just that section instead of the whole file.</p>
        </div>
        <div class="rounded-lg border border-border bg-card p-4">
          <div class="flex items-center gap-2 mb-1">
            <Brain :size="16" class="text-muted-foreground" />
            <h3 class="text-sm font-medium">SM-2 scheduling</h3>
          </div>
          <p class="text-xs text-muted-foreground">Proven spaced repetition algorithm. Rate recall and the schedule adapts.</p>
        </div>
        <div class="rounded-lg border border-border bg-card p-4">
          <div class="flex items-center gap-2 mb-1">
            <FolderOpen :size="16" class="text-muted-foreground" />
            <h3 class="text-sm font-medium">Groups</h3>
          </div>
          <p class="text-xs text-muted-foreground">Organize cards into groups for focused study sessions.</p>
        </div>
        <div class="rounded-lg border border-border bg-card p-4">
          <div class="flex items-center gap-2 mb-1">
            <BarChart3 :size="16" class="text-muted-foreground" />
            <h3 class="text-sm font-medium">Dashboard</h3>
          </div>
          <p class="text-xs text-muted-foreground">Cards due, total cards, and study streaks at a glance.</p>
        </div>
        <div class="rounded-lg border border-border bg-card p-4">
          <div class="flex items-center gap-2 mb-1">
            <FileText :size="16" class="text-muted-foreground" />
            <h3 class="text-sm font-medium">Rendered markdown</h3>
          </div>
          <p class="text-xs text-muted-foreground">Cards render with full formatting — code blocks, lists, emphasis.</p>
        </div>
      </div>
    </section>

    <!-- CTA -->
    <section class="border-t border-border bg-muted/30">
      <div class="mx-auto max-w-5xl px-6 py-12 text-center">
        <h2 class="text-2xl font-semibold mb-2">Start retaining what you read</h2>
        <p class="text-sm text-muted-foreground mb-5">Free and open source. Your notes, your cards.</p>
        <RouterLink to="/register">
          <Button class="gap-2">
            Create an account
            <ArrowRight :size="16" />
          </Button>
        </RouterLink>
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
