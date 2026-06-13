<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { Bot, Layers, FileText, Brain, BarChart3, Image, Search, Server } from 'lucide-vue-next'
import ToolSwitcher from '@/components/ToolSwitcher.vue'
import FasoltStudyPreview from '@/components/FasoltStudyPreview.vue'
import FasoltWordmark from '@/components/FasoltWordmark.vue'
import ThemeToggle from '@/components/ThemeToggle.vue'
import AppFooter from '@/components/AppFooter.vue'
import { ArrowRight, ArrowDown } from 'lucide-vue-next'
</script>

<template>
  <div class="min-h-screen bg-paper-0 text-ink-0">
    <!-- Subtle dotted grid background -->
    <div class="pointer-events-none fixed inset-0 -z-10 bg-grid bg-grid-fade opacity-50" />

    <!-- Nav -->
    <nav class="sticky top-0 z-50 border-b border-rule-1 bg-paper-0">
      <div class="mx-auto flex max-w-5xl items-center justify-between px-6 py-4">
        <FasoltWordmark :size="32" />
        <div class="flex items-center gap-2">
          <ThemeToggle />
          <a href="/login" class="fa-btn fa-btn-ghost">Log in</a>
        </div>
      </div>
    </nav>

    <!-- Hero -->
    <section class="relative mx-auto max-w-5xl px-6 pt-6 pb-12 sm:pt-10 sm:pb-16">
      <div class="max-w-2xl">
        <p class="fa-cap mb-4 text-accent-text">Spaced repetition</p>
        <h1 class="page-title text-3xl sm:text-5xl mb-5">
          Spaced repetition,<br class="hidden sm:block" />
          powered by the <span class="text-accent-text">AI you already use</span>.
        </h1>
        <p class="text-[15px] text-ink-1 mb-8 max-w-md leading-relaxed">
          Ask ChatGPT, Claude, or Mistral to make flashcards from your notes, a topic, or anything you want to learn.
          Study on the web or the iOS app. Free.
        </p>
        <div class="flex flex-wrap items-center gap-3">
          <a href="/register" class="fa-btn fa-btn-primary">Get started</a>
          <a href="/login" class="fa-btn">Log in</a>
        </div>
        <p class="mt-4 text-xs text-ink-2">
          Works with ChatGPT, Claude, Mistral, and any MCP-compatible AI.
        </p>
      </div>
    </section>

    <!-- Tool switcher demo + study preview -->
    <section class="relative mx-auto max-w-6xl px-6 pb-20">
      <div class="grid gap-4 lg:grid-cols-[1fr_auto_1fr] lg:gap-4">
        <div class="flex min-w-0 flex-col">
          <div class="mb-3">
            <span class="inline-block rounded-full bg-accent-soft px-2.5 py-0.5 text-[11px] font-semibold text-accent-text">Step 1</span>
            <h3 class="mt-2 text-base font-semibold text-ink-0">Ask your AI to make cards</h3>
            <p class="mt-0.5 text-[12.5px] text-ink-2">From your notes, a topic, anything you want to learn.</p>
          </div>
          <ToolSwitcher />
        </div>
        <div class="hidden lg:flex items-center justify-center text-ink-3">
          <ArrowRight :size="24" />
        </div>
        <div class="flex justify-center lg:hidden text-ink-3 -my-1">
          <ArrowDown :size="18" />
        </div>
        <div class="flex min-w-0 flex-col">
          <div class="mb-3">
            <span class="inline-block rounded-full bg-accent-soft px-2.5 py-0.5 text-[11px] font-semibold text-accent-text">Step 2</span>
            <h3 class="mt-2 text-base font-semibold text-ink-0">Study them in fasolt</h3>
            <p class="mt-0.5 text-[12.5px] text-ink-2">On the web or the iOS app.</p>
          </div>
          <FasoltStudyPreview />
          <p class="mt-3 text-xs text-ink-2">
            Cards are scheduled with FSRS — the more you remember, the longer the gaps.
          </p>
        </div>
      </div>
    </section>

    <!-- How it works -->
    <section class="border-y border-rule-1 bg-paper-1">
      <div class="mx-auto max-w-5xl px-6 py-16">
        <p class="fa-cap mb-8">How it works</p>
        <div class="grid gap-10 sm:grid-cols-3">
          <div>
            <span class="fa-num text-accent-text mb-2 block text-xs">01</span>
            <h3 class="text-sm font-semibold mb-2 text-ink-0">Connect your AI</h3>
            <p class="text-xs text-ink-1 leading-relaxed">
              Add fasolt to ChatGPT, Claude, Mistral, or any AI tool that supports connectors.
            </p>
          </div>
          <div>
            <span class="fa-num text-accent-text mb-2 block text-xs">02</span>
            <h3 class="text-sm font-semibold mb-2 text-ink-0">Create flashcards</h3>
            <p class="text-xs text-ink-1 leading-relaxed">
              Ask your agent to make cards from your notes, a topic, or anything you want to learn.
            </p>
          </div>
          <div>
            <span class="fa-num text-accent-text mb-2 block text-xs">03</span>
            <h3 class="text-sm font-semibold mb-2 text-ink-0">Learn and remember</h3>
            <p class="text-xs text-ink-1 leading-relaxed">
              Study your cards on the web or the iOS app. Spaced repetition schedules reviews at optimal intervals.
            </p>
          </div>
        </div>
      </div>
    </section>

    <!-- Features -->
    <section class="mx-auto max-w-5xl px-6 py-16">
      <p class="fa-cap mb-8">Features</p>
      <div class="grid gap-x-10 gap-y-8 sm:grid-cols-2 lg:grid-cols-4">
        <div>
          <Bot :size="18" class="text-accent mb-2" />
          <h3 class="text-sm font-semibold mb-1 text-ink-0">Bring your own AI</h3>
          <p class="text-xs text-ink-1 leading-relaxed">Use ChatGPT, Claude, Mistral, or any MCP-compatible agent to create and manage cards.</p>
        </div>
        <div>
          <Brain :size="18" class="text-accent mb-2" />
          <h3 class="text-sm font-semibold mb-1 text-ink-0">FSRS scheduling</h3>
          <p class="text-xs text-ink-1 leading-relaxed">Optimal review intervals backed by research. <RouterLink to="/algorithm" class="text-accent-text hover:underline">Learn more</RouterLink></p>
        </div>
        <div>
          <Layers :size="18" class="text-accent mb-2" />
          <h3 class="text-sm font-semibold mb-1 text-ink-0">Decks</h3>
          <p class="text-xs text-ink-1 leading-relaxed">Organize cards into focused study groups.</p>
        </div>
        <div>
          <FileText :size="18" class="text-accent mb-2" />
          <h3 class="text-sm font-semibold mb-1 text-ink-0">Source tracking</h3>
          <p class="text-xs text-ink-1 leading-relaxed">Cards retain context via file and heading.</p>
        </div>
        <div>
          <BarChart3 :size="18" class="text-accent mb-2" />
          <h3 class="text-sm font-semibold mb-1 text-ink-0">Dashboard</h3>
          <p class="text-xs text-ink-1 leading-relaxed">Stats, due counts, and study streaks.</p>
        </div>
        <div>
          <Image :size="18" class="text-accent mb-2" />
          <h3 class="text-sm font-semibold mb-1 text-ink-0">SVG support</h3>
          <p class="text-xs text-ink-1 leading-relaxed">Diagrams and visualizations on cards.</p>
        </div>
        <div>
          <Search :size="18" class="text-accent mb-2" />
          <h3 class="text-sm font-semibold mb-1 text-ink-0">Search</h3>
          <p class="text-xs text-ink-1 leading-relaxed">Full-text search across all your cards.</p>
        </div>
        <div>
          <Server :size="18" class="text-accent mb-2" />
          <h3 class="text-sm font-semibold mb-1 text-ink-0">Self-hostable</h3>
          <p class="text-xs text-ink-1 leading-relaxed">Run your own instance with Docker.</p>
        </div>
      </div>
    </section>

    <!-- iOS showcase -->
    <section class="mx-auto max-w-5xl px-6 py-16">
      <div class="flex flex-col items-center gap-6">
        <p class="fa-cap">Study anywhere</p>
        <a href="https://apps.apple.com/us/app/fasolt/id6761048313" target="_blank" rel="noopener" class="fa-btn">
          Download on the App Store
        </a>
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
      <p class="fa-cap mb-4">Free and open source</p>
      <a href="/register" class="fa-btn fa-btn-primary">Get started</a>
    </section>

    <AppFooter />
  </div>
</template>
