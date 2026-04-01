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
      <p class="text-xs text-muted-foreground mt-1">Last updated: April 1, 2026</p>
    </div>

    <div class="space-y-6 text-xs text-muted-foreground leading-relaxed">
      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">1. Controller</h2>
        <p>The controller responsible for data processing on this website is:</p>
        <address class="not-italic space-y-1">
          <p>Philipp Baum</p>
          <p>c/o Webix Solutions GmbH</p>
          <p>Zuckerfabrik 14</p>
          <p>70376 Stuttgart, Germany</p>
          <p>Email: <a href="mailto:info@fasolt.app" class="text-accent hover:underline">info@fasolt.app</a></p>
        </address>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">2. What We Collect and Legal Basis</h2>
        <p>When you create an account, we collect your email address and a hashed password. We never store your password in plain text. This processing is necessary to perform our contract with you (Art. 6(1)(b) GDPR).</p>
        <p>When you use the service, we store the flashcards you create (front text, back text, source file name, heading) and your review history (timestamps, ratings, scheduling data). No markdown files or note contents are stored on our servers beyond the card text you explicitly send via the API or MCP tools. This processing is necessary to perform our contract with you (Art. 6(1)(b) GDPR).</p>
        <p>We collect basic server logs (IP address, request timestamps, user agent) for security and debugging purposes, based on our legitimate interest in maintaining the security and stability of the service (Art. 6(1)(f) GDPR). Server logs are retained for up to 90 days.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">3. What We Do Not Collect</h2>
        <p>We do not store your local files. Your markdown notes remain on your device. Only the flashcard content you explicitly push via the API or MCP server is sent to us.</p>
        <p>We do not use third-party analytics, tracking pixels, or advertising cookies. We do not sell, rent, or share your data with third parties.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">4. How We Use Your Data</h2>
        <p>Your data is used solely to provide the spaced repetition service: storing your cards, scheduling reviews, and tracking your study progress.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">5. Data Storage and Security</h2>
        <p>Your data is stored in a PostgreSQL database hosted in Germany. Passwords are hashed using industry-standard algorithms. All traffic between your browser and our servers is encrypted via HTTPS. Authentication tokens and sessions are secured with standard ASP.NET Core Identity protections.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">6. Data Processors and Third Parties</h2>
        <p>We use Cloudflare as a CDN and security provider. Cloudflare may process request metadata (IP addresses, request headers) on our behalf. Cloudflare is certified under the EU-U.S. Data Privacy Framework. For details, see <a href="https://www.cloudflare.com/privacypolicy/" target="_blank" rel="noopener noreferrer" class="text-accent hover:underline">Cloudflare's privacy policy</a>.</p>
        <p>If you connect via an AI agent (e.g., Claude, Cursor), the agent sends card data to our API on your behalf. We do not control what the agent retains locally. Refer to your agent's privacy policy for how it handles your data.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">7. Cookies</h2>
        <p>We use a single authentication cookie to keep you logged in. This cookie is strictly necessary for the service to function and does not require consent (Art. 6(1)(b) GDPR). We do not use tracking cookies, advertising cookies, or any third-party cookies.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">8. Data Retention</h2>
        <p>Account data, flashcards, and review history are stored for as long as your account exists. Server logs are retained for up to 90 days. When you delete your account, all your cards, review history, and account information are permanently removed.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">9. Your Rights</h2>
        <p>Under the GDPR, you have the right to:</p>
        <ul class="list-disc list-inside space-y-1 pl-2">
          <li><strong class="text-foreground">Access</strong> your personal data (Art. 15 GDPR)</li>
          <li><strong class="text-foreground">Rectification</strong> of inaccurate data (Art. 16 GDPR)</li>
          <li><strong class="text-foreground">Erasure</strong> of your data (Art. 17 GDPR)</li>
          <li><strong class="text-foreground">Restriction</strong> of processing (Art. 18 GDPR)</li>
          <li><strong class="text-foreground">Data portability</strong> — receive your data in a structured, machine-readable format (Art. 20 GDPR)</li>
          <li><strong class="text-foreground">Object</strong> to processing based on legitimate interest (Art. 21 GDPR)</li>
        </ul>
        <p>To exercise these rights, contact us at <a href="mailto:info@fasolt.app" class="text-accent hover:underline">info@fasolt.app</a> or use the account deletion option in Settings.</p>
        <p>You also have the right to lodge a complaint with a supervisory authority. The competent authority is the <strong class="text-foreground">Landesbeauftragte für den Datenschutz und die Informationsfreiheit Baden-Württemberg</strong> (<a href="https://www.baden-wuerttemberg.datenschutz.de" target="_blank" rel="noopener noreferrer" class="text-accent hover:underline">www.baden-wuerttemberg.datenschutz.de</a>).</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">10. Children</h2>
        <p>This service is not directed at children under 16. We do not knowingly collect data from children under 16.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">11. Changes to This Policy</h2>
        <p>We may update this policy from time to time. Changes will be posted on this page with an updated date. We will notify registered users of material changes via email.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">12. Contact</h2>
        <p>For questions about this privacy policy or your data, email us at <a href="mailto:info@fasolt.app" class="text-accent hover:underline">info@fasolt.app</a>.</p>
      </section>
    </div>
  </div>
    <AppFooter />
  </div>
</template>
