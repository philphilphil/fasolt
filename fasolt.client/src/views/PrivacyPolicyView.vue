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
        <h2 class="text-sm font-semibold text-foreground">2. What We Process and Why</h2>
        <p>When you create and use an account, we process account and access data such as your email address, hashed password, email confirmation status, authentication/session data, and security-related request data needed to protect login and API flows. This processing is necessary to provide the service and protect it against abuse (Art. 6(1)(b) and Art. 6(1)(f) GDPR).</p>
        <p>When you use fasolt, we process the content and metadata you send to us, including card fronts and backs, SVGs, deck data, source file names, source headings, review history, scheduling values, and account settings. If you authorize an OAuth or MCP client, we also store the client identifier and the time you granted consent. This processing is necessary to provide the service you requested (Art. 6(1)(b) GDPR).</p>
        <p>If you sign in with GitHub, we process the GitHub account identifier and username provided during the login flow and create an internal account record for your fasolt account. This processing takes place at your request to provide external login functionality (Art. 6(1)(b) GDPR).</p>
        <p>If you enable push notifications, we process your device token, reminder settings, and the notification content needed to deliver reminders through Apple Push Notification service (APNs). This processing is necessary to provide the reminder function you requested (Art. 6(1)(b) GDPR and, where required, based on your device-level consent).</p>
        <p>When we send account-related emails such as verification or password reset emails, we process your email address and the message content needed to deliver those emails. This processing is necessary to operate your account and communicate service-related information to you (Art. 6(1)(b) GDPR).</p>
        <p>We also process technical logs and, if enabled in the relevant deployment, error-monitoring data to maintain security, availability, and troubleshoot incidents. This is based on our legitimate interest in operating a secure and reliable service (Art. 6(1)(f) GDPR).</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">3. What We Do Not Store</h2>
        <p>We do not store your local markdown files as files. Your notes remain on your device unless you or your client explicitly send derived card content and metadata to us through the web app, API, or MCP server.</p>
        <p>We do not use advertising cookies, third-party tracking pixels, or sell your personal data.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">4. Required and Optional Data</h2>
        <p>Providing the data needed to create an account and store cards is necessary if you want to use fasolt. If you do not provide the required account or service data, we cannot create or operate your account.</p>
        <p>GitHub login and push notifications are optional. You can use email/password login instead of GitHub, and you can use fasolt without enabling push notifications.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">5. Recipients, Processors, and External Services</h2>
        <p>We use service providers and infrastructure components to operate fasolt. Depending on the feature you use, personal data may be processed by the following recipients or categories of recipients:</p>
        <ul class="list-disc list-inside space-y-1 pl-2">
          <li><strong class="text-foreground">Cloudflare</strong> for edge delivery and security of the production service</li>
          <li><strong class="text-foreground">Plunk</strong> for transactional email delivery</li>
          <li><strong class="text-foreground">GitHub</strong> if you choose GitHub sign-in</li>
          <li><strong class="text-foreground">Apple APNs</strong> if you enable iOS push notifications</li>
          <li><strong class="text-foreground">Error-monitoring providers</strong> using a Sentry-compatible setup, if enabled for the relevant deployment</li>
          <li><strong class="text-foreground">Hosting, database, and technical infrastructure providers</strong> as needed to run the service</li>
        </ul>
        <p>If you connect through an AI agent or third-party client, that client sends data to us on your behalf. We do not control what that client provider stores or processes on its own systems. Please review the privacy information of the client or agent you choose to use.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">6. International Data Transfers</h2>
        <p>Some of the providers listed above may process personal data outside the EU or EEA, including in the United States. Where this happens, we rely on an adequacy decision, Standard Contractual Clauses, or other lawful transfer mechanism required by applicable law, together with supplementary safeguards where necessary.</p>
        <p>You can contact us at <a href="mailto:info@fasolt.app" class="text-accent hover:underline">info@fasolt.app</a> if you would like more information about the transfer safeguards relevant to a particular processing activity.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">7. Cookies and Local Storage</h2>
        <p>We use cookies and similar browser storage for essential service functions such as keeping you signed in, completing login and OAuth flows, and protecting security-sensitive requests. We also store your light/dark theme preference in your browser's local storage.</p>
        <p>We do not use advertising cookies or third-party tracking pixels.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">8. Data Retention</h2>
        <p>Account data, cards, decks, review history, consent grants, and notification settings are stored for as long as your account exists or until you delete them through the service where applicable.</p>
        <p>Device tokens are stored until you remove them, disable the related feature, or delete your account. Technical logs and application logs are retained only as long as necessary for security, abuse prevention, debugging, and operational purposes.</p>
        <p>When you delete your account, the account and associated service data stored by us are deleted, subject only to any limited retention required by law or unavoidable short-term technical remnants such as transient backups.</p>
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
        <p>You can exercise these rights by contacting us at <a href="mailto:info@fasolt.app" class="text-accent hover:underline">info@fasolt.app</a>. Where available, you can also use in-product export and account deletion features.</p>
        <p>We do not use automated decision-making with legal or similarly significant effects within the meaning of Art. 22 GDPR. Review scheduling is automated, but it only affects study timing inside the service.</p>
        <p>You also have the right to lodge a complaint with a supervisory authority. The competent authority is the <strong class="text-foreground">Landesbeauftragte für den Datenschutz und die Informationsfreiheit Baden-Württemberg</strong> (<a href="https://www.baden-wuerttemberg.datenschutz.de" target="_blank" rel="noopener noreferrer" class="text-accent hover:underline">www.baden-wuerttemberg.datenschutz.de</a>).</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">10. Security</h2>
        <p>We use technical and organizational measures designed to protect your data, including encrypted transport in production, hashed passwords, and access controls. No method of transmission or storage is completely risk-free, but we work to protect the data we process appropriately.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">11. Children</h2>
        <p>This service is not directed at children under 16. We do not knowingly collect data from children under 16.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">12. Changes to This Policy</h2>
        <p>We may update this policy from time to time. Changes will be posted on this page with an updated date. Where legally required or appropriate, we will also inform registered users of material changes by email or through the service.</p>
      </section>

      <section class="space-y-2">
        <h2 class="text-sm font-semibold text-foreground">13. Contact</h2>
        <p>For questions about this privacy policy or your data, email us at <a href="mailto:info@fasolt.app" class="text-accent hover:underline">info@fasolt.app</a>.</p>
      </section>
    </div>
  </div>
    <AppFooter />
  </div>
</template>
