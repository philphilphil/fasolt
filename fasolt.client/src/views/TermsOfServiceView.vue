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
        <h1 class="text-lg font-semibold tracking-tight">Terms of Service</h1>
        <p class="mt-1 text-xs text-muted-foreground">Last updated: March 31, 2026</p>
      </div>

      <div class="space-y-6 text-xs leading-relaxed text-muted-foreground">
        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">1. Scope</h2>
          <p>These Terms of Service govern your use of fasolt, including the website, API, MCP endpoint, and related applications.</p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">2. Service description</h2>
          <p>fasolt is a spaced repetition service for flashcards generated from your notes. Your AI agent or client can create and manage cards through the web UI, API, or MCP tools.</p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">3. Accounts</h2>
          <p>You are responsible for maintaining the confidentiality of your login credentials and for activities that occur under your account.</p>
          <p>You must provide accurate registration information and keep it up to date.</p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">4. Acceptable use</h2>
          <p>You may not use the service to violate applicable law, interfere with the availability or security of the service, or submit content you do not have the right to use.</p>
          <p>You remain responsible for the flashcard content and metadata you send to fasolt.</p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">5. Availability and changes</h2>
          <p>The service is provided on an as-available basis. We may change, suspend, or discontinue parts of the service if reasonably necessary for maintenance, security, or product development.</p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">6. Intellectual property</h2>
          <p>fasolt and its branding remain the property of their respective owners. You retain rights to the content you create and upload or send through the service.</p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">7. Termination</h2>
          <p>We may suspend or terminate access if you materially violate these terms or if this is necessary to protect the service, other users, or legal compliance.</p>
          <p>You may stop using the service at any time and may request deletion of your account and stored data.</p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">8. Liability</h2>
          <p>To the extent permitted by law, fasolt is not liable for indirect, incidental, or consequential damages arising from use of the service. Mandatory statutory liability, including liability for intent, gross negligence, or injury to life, body, or health, remains unaffected.</p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">9. Privacy</h2>
          <p>
            Our processing of personal data is described in the
            <RouterLink to="/privacy" class="text-accent hover:underline">Privacy Policy</RouterLink>.
          </p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">10. Governing law</h2>
          <p>These terms are governed by the laws of the Federal Republic of Germany, excluding conflict-of-law rules, unless mandatory consumer protection law provides otherwise.</p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">11. Contact</h2>
          <p>
            For questions about these terms, please refer to the
            <RouterLink to="/impressum" class="text-accent hover:underline">Impressum</RouterLink>
            or open an issue on the
            <a
              href="https://github.com/philphilphil/fasolt"
              target="_blank"
              rel="noopener noreferrer"
              class="text-accent hover:underline"
            >
              GitHub repository
            </a>.
          </p>
        </section>
      </div>
    </div>
    <AppFooter />
  </div>
</template>
