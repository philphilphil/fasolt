<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'

const version = ref('')

onMounted(async () => {
  try {
    const res = await fetch('/api/health')
    if (res.ok) {
      const data = await res.json()
      version.value = data.version ?? ''
    }
  } catch { /* ignore */ }
})
</script>

<template>
  <footer class="app-footer">
    <div class="footer-left">
      <a href="https://github.com/philphilphil/fasolt" target="_blank" rel="noopener noreferrer" aria-label="GitHub" class="footer-gh">
        <svg class="size-4" viewBox="0 0 24 24" fill="currentColor"><path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0 0 24 12c0-6.63-5.37-12-12-12z"/></svg>
      </a>
      <span v-if="version" class="fa-mono">fasolt v{{ version }}</span>
      <RouterLink to="/algorithm" class="footer-link">FSRS algorithm</RouterLink>
    </div>
    <span class="footer-center">Made & hosted in the EU</span>
    <div class="footer-right">
      <RouterLink to="/terms" class="footer-link">Terms</RouterLink>
      <RouterLink to="/privacy" class="footer-link">Privacy</RouterLink>
      <RouterLink to="/impressum" class="footer-link">Impressum</RouterLink>
    </div>
  </footer>
</template>

<style scoped>
.app-footer {
  border-top: 1px solid var(--rule-1);
  padding: 12px 20px;
  font-size: 11.5px;
  color: var(--ink-2);
  display: grid;
  grid-template-columns: 1fr auto 1fr;
  align-items: center;
  gap: 16px;
}
.footer-left {
  display: flex;
  align-items: center;
  gap: 14px;
}
.footer-center {
  text-align: center;
  color: var(--ink-3);
}
.footer-right {
  display: flex;
  align-items: center;
  gap: 14px;
  justify-content: flex-end;
}
.footer-gh {
  color: var(--ink-3);
  transition: color .12s;
}
.footer-gh:hover { color: var(--ink-0); }
.footer-link {
  color: var(--ink-2);
  text-decoration: none;
  transition: color .12s;
}
.footer-link:hover { color: var(--ink-0); }
</style>
