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
    <div class="mx-auto max-w-3xl px-6 py-12 space-y-8">
    <div>
      <h1 class="text-lg font-semibold tracking-tight">Privacy Policy</h1>
      <p class="text-xs text-muted-foreground mt-1">Last updated: March 26, 2026</p>
    </div>

    <div class="space-y-6 text-xs text-muted-foreground leading-relaxed">
      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">1. What We Collect</h2>
        <p>When you create an account, we collect your email address and a hashed password. We never store your password in plain text.</p>
        <p>When you use the service, we store the flashcards you create (front text, back text, source file name, heading) and your review history (timestamps, ratings, scheduling data). No markdown files or note contents are stored on our servers beyond the card text you explicitly send via the API or MCP tools.</p>
        <p>We collect basic server logs (IP address, request timestamps, user agent) for security and debugging purposes.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">2. What We Do Not Collect</h2>
        <p>We do not store your local files. Your markdown notes remain on your device. Only the flashcard content you explicitly push via the API or MCP server is sent to us.</p>
        <p>We do not use third-party analytics, tracking pixels, or advertising cookies. We do not sell, rent, or share your data with third parties.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">3. How We Use Your Data</h2>
        <p>Your data is used solely to provide the spaced repetition service: storing your cards, scheduling reviews, and tracking your study progress. Server logs are used for security monitoring and debugging, and are retained for up to 90 days.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">4. Data Storage and Security</h2>
        <p>Your data is stored in a PostgreSQL database. Passwords are hashed using industry-standard algorithms. All traffic between your browser and our servers is encrypted via HTTPS. Authentication tokens and sessions are secured with standard ASP.NET Core Identity protections.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">5. Cookies</h2>
        <p>We use a single authentication cookie to keep you logged in. We do not use tracking cookies, advertising cookies, or any third-party cookies.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">6. Your Rights</h2>
        <p>You can export or delete your data at any time. To delete your account and all associated data, contact us or use the account deletion option in Settings. Upon deletion, all your cards, review history, and account information are permanently removed.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">7. Third-Party Services</h2>
        <p>If you connect via an AI agent (e.g., Claude, Cursor), the agent sends card data to our API on your behalf. We do not control what the agent retains locally. Refer to your agent's privacy policy for how it handles your data.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">8. Children</h2>
        <p>This service is not directed at children under 13. We do not knowingly collect data from children under 13.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">9. Changes to This Policy</h2>
        <p>We may update this policy from time to time. Changes will be posted on this page with an updated date. Continued use of the service after changes constitutes acceptance.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">10. Contact</h2>
        <p>For questions about this privacy policy or your data, open an issue on our GitHub repository or email the project maintainer.</p>
      </section>
    </div>
  </div>
    <AppFooter />
  </div>
</template>
