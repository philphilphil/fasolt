<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useSourcesStore } from '@/stores/sources'

const router = useRouter()
const sourcesStore = useSourcesStore()

onMounted(() => sourcesStore.fetchSources())
</script>

<template>
  <div class="sources-page">
    <div>
      <h1 class="page-title">Sources</h1>
      <p class="page-sub">Files your cards were created from.</p>
    </div>

    <div v-if="sourcesStore.loading" class="empty">Loading…</div>

    <div v-else-if="sourcesStore.sources.length === 0" class="empty">
      No sources yet. Use the MCP agent to create cards from your markdown notes.
    </div>

    <div v-else class="source-list">
      <button
        v-for="source in sourcesStore.sources"
        :key="source.sourceFile"
        class="source-row"
        @click="router.push(`/cards?sourceFile=${encodeURIComponent(source.sourceFile)}`)"
      >
        <div class="source-row-text">
          <div class="source-row-name">{{ source.sourceFile }}</div>
          <div class="source-row-sub">{{ source.cardCount }} cards</div>
        </div>
        <div v-if="source.dueCount > 0" class="source-row-due fa-num">{{ source.dueCount }} due</div>
        <svg class="source-chevron" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m9 6 6 6-6 6"/></svg>
      </button>
    </div>
  </div>
</template>

<style scoped>
.sources-page {
  padding: 40px 8px 48px;
  max-width: 760px;
  margin: 0 auto;
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: 24px;
}
.page-sub {
  margin: 6px 0 0;
  font-size: 13.5px;
  color: var(--ink-2);
}

/* Source list — rows on the page, hairline separators, no outer box */
.source-list { display: flex; flex-direction: column; }
.source-row {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 15px 4px;
  width: 100%;
  text-align: left;
  background: transparent;
  border: none;
  border-bottom: 1px solid var(--rule-1);
  cursor: pointer;
  color: inherit;
  font: inherit;
  transition: background .12s;
}
.source-row:last-child { border-bottom: none; }
.source-row:hover { background: var(--paper-2); }
.source-row-text { flex: 1; min-width: 0; }
.source-row-name {
  font-size: 15.5px;
  color: var(--ink-0);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.source-row-sub { font-size: 12.5px; color: var(--ink-2); margin-top: 1px; }
.source-row-due { font-size: 14px; color: var(--accent-text); font-weight: 600; flex: none; }
.source-chevron { color: var(--ink-3); flex: none; }

/* Empty / loading state */
.empty {
  padding: 24px 0;
  color: var(--ink-2);
  font-size: 13.5px;
}
</style>
