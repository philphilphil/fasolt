<script setup lang="ts">
import { RouterLink } from 'vue-router'
import AppFooter from '@/components/AppFooter.vue'
</script>

<template>
  <div class="min-h-screen bg-background text-foreground">
    <nav class="sticky top-0 z-50 border-b border-border/60 bg-background/80 backdrop-blur-md">
      <div class="mx-auto flex max-w-3xl items-center px-6 py-4">
        <RouterLink to="/" class="flex items-center gap-2.5 text-sm font-bold tracking-tight">
          <img src="/logo.svg" alt="fasolt" class="h-10 object-contain" />
          fasolt
        </RouterLink>
      </div>
    </nav>

    <div class="mx-auto max-w-3xl space-y-8 px-6 py-12">
      <div>
        <h1 class="text-lg font-semibold tracking-tight">Impressum</h1>
        <p class="mt-1 text-xs text-muted-foreground">Information according to Section 5 TMG</p>
      </div>

      <div class="space-y-6 text-xs leading-relaxed text-muted-foreground">
        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">Service provider</h2>
          <address class="not-italic space-y-1">
            <p>Philipp Baum</p>
            <p>Webix Solutions GmbH</p>
            <p>Zuckerfabrik 14</p>
            <p>70376 Stuttgart</p>
            <p>Germany</p>
          </address>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">Contact</h2>
          <p>
            Please use the
            <a
              href="https://github.com/philphilphil/fasolt"
              target="_blank"
              rel="noopener noreferrer"
              class="text-accent hover:underline"
            >
              GitHub repository
            </a>
            for project-related inquiries.
          </p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">Responsible for content</h2>
          <p>Philipp Baum, address as above.</p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">EU dispute resolution</h2>
          <p>
            The European Commission provides a platform for online dispute resolution (ODR):
            <a
              href="https://ec.europa.eu/consumers/odr/"
              target="_blank"
              rel="noopener noreferrer"
              class="text-accent hover:underline"
            >
              https://ec.europa.eu/consumers/odr/
            </a>
          </p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">Consumer dispute resolution</h2>
          <p>We are not willing or obliged to participate in dispute resolution proceedings before a consumer arbitration board.</p>
        </section>
      </div>
    </div>
    <AppFooter />
  </div>
</template>
