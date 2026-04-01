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
        <p class="mt-1 text-xs text-muted-foreground">Last updated: April 1, 2026</p>
      </div>

      <div class="space-y-6 text-xs leading-relaxed text-muted-foreground">
        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">1. Scope</h2>
          <p>These Terms of Service govern your use of fasolt, including the website, API, MCP endpoint, and related applications. They apply to all users, whether consumers or businesses.</p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">2. Service description</h2>
          <p>fasolt is a spaced repetition service for flashcards generated from your notes. Your AI agent or client can create and manage cards through the web UI, API, or MCP tools. The service is currently provided free of charge.</p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">3. Accounts</h2>
          <p>You are responsible for maintaining the confidentiality of your login credentials and for activities that occur under your account.</p>
          <p>You must provide accurate registration information and keep it up to date.</p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">4. Right of withdrawal</h2>
          <p>As a consumer in the EU, you have a 14-day right of withdrawal from the date of registration. Because fasolt is a free digital service that begins immediately upon registration, you consent to the immediate provision of the service and acknowledge that your right of withdrawal expires once the service has been fully provided (§ 356 Abs. 4 BGB).</p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">5. Acceptable use</h2>
          <p>You may not use the service to violate applicable law, interfere with the availability or security of the service, or submit content you do not have the right to use.</p>
          <p>You remain responsible for the flashcard content and metadata you send to fasolt.</p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">6. Availability and changes</h2>
          <p>The service is provided on an as-available basis without guaranteed uptime. We may modify or discontinue features when reasonably necessary for security, legal compliance, or technical reasons. We will notify you of material changes in advance where practicable. If a change significantly reduces the functionality of the service, you may terminate your account.</p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">7. Intellectual property</h2>
          <p>fasolt and its branding remain the property of their respective owners. You retain rights to the content you create and upload or send through the service.</p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">8. Termination</h2>
          <p>We may suspend or terminate access if you materially violate these terms or if this is necessary to protect the service, other users, or legal compliance.</p>
          <p>You may stop using the service at any time and may request deletion of your account and stored data.</p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">9. Liability</h2>
          <p>To the extent permitted by law, liability for slight negligence is excluded, except for damages arising from injury to life, body, or health. Mandatory statutory liability, including liability for intent and gross negligence, remains unaffected.</p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">10. Privacy</h2>
          <p>
            Our processing of personal data is described in the
            <RouterLink to="/privacy" class="text-accent hover:underline">Privacy Policy</RouterLink>.
          </p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">11. Governing law and jurisdiction</h2>
          <p>These terms are governed by the laws of the Federal Republic of Germany, excluding conflict-of-law rules and the UN Convention on Contracts for the International Sale of Goods (CISG).</p>
          <p>If you are a consumer habitually resident in the EU, you also benefit from any mandatory provisions of the law of your country of residence. For consumers, the courts of your place of residence have jurisdiction. For all other users, the courts of Stuttgart, Germany have exclusive jurisdiction.</p>
        </section>

        <section class="space-y-2">
          <h2 class="text-sm font-semibold text-foreground">12. Contact</h2>
          <p>
            For questions about these terms, email us at
            <a href="mailto:info@fasolt.app" class="text-accent hover:underline">info@fasolt.app</a>
            or refer to the
            <RouterLink to="/impressum" class="text-accent hover:underline">Impressum</RouterLink>.
          </p>
        </section>
      </div>
    </div>
    <AppFooter />
  </div>
</template>
