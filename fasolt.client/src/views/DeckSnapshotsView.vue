<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter, RouterLink } from 'vue-router'
import { useSnapshotsStore } from '@/stores/snapshots'
import { useDecksStore } from '@/stores/decks'
import type { DeckDetail } from '@/types'

const route = useRoute()
const router = useRouter()
const snapshotsStore = useSnapshotsStore()
const decksStore = useDecksStore()

const deckId = route.params.id as string
const deck = ref<DeckDetail | null>(null)
const loading = ref(true)

onMounted(async () => {
  try {
    const [d] = await Promise.all([
      decksStore.getDeckDetail(deckId),
      snapshotsStore.fetchByDeck(deckId),
    ])
    deck.value = d
  } catch {
    router.replace('/decks')
  } finally {
    loading.value = false
  }
})

async function handleDelete(snapshotId: string) {
  if (!confirm('Delete this snapshot? This cannot be undone.')) return
  await snapshotsStore.deleteSnapshot(snapshotId)
  await snapshotsStore.fetchByDeck(deckId)
}

function formatDate(iso: string) {
  const d = new Date(iso)
  return d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
    + ' at '
    + d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })
}
</script>

<template>
  <div v-if="loading" class="loading">Loading…</div>

  <div v-else class="snapshots-page">
    <RouterLink :to="`/decks/${deckId}`" class="fa-back">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m15 18-6-6 6-6"/></svg>
      {{ deck?.name || 'Deck' }}
    </RouterLink>

    <!-- Header -->
    <header class="page-head">
      <div>
        <h1 class="page-title">Snapshots</h1>
        <p class="page-sub">Restore {{ deck?.name }} to a previous state</p>
      </div>
      <button class="fa-btn" @click="router.push(`/decks/${deckId}`)">Back to deck</button>
    </header>

    <!-- Snapshots list -->
    <div v-if="snapshotsStore.snapshots.length > 0" class="snap-list">
      <div
        v-for="snapshot in snapshotsStore.snapshots"
        :key="snapshot.id"
        class="snap-row"
      >
        <div class="snap-text">
          <div class="snap-date fa-mono fa-num">{{ formatDate(snapshot.createdAt) }}</div>
          <div class="snap-meta">
            {{ snapshot.cardCount }} cards
            <span class="snap-dot">·</span>
            <span v-if="snapshot.contentChanges === 0">no content differences</span>
            <span v-else-if="snapshot.contentChanges != null" class="snap-changes">{{ snapshot.contentChanges }} content difference{{ snapshot.contentChanges !== 1 ? 's' : '' }}</span>
          </div>
        </div>
        <div class="snap-actions">
          <button
            class="fa-btn"
            @click="router.push(`/decks/${deckId}/snapshots/${snapshot.id}/restore`)"
          >
            Restore
          </button>
          <button
            class="fa-btn fa-btn-ghost snap-delete"
            @click="handleDelete(snapshot.id)"
          >
            Delete
          </button>
        </div>
      </div>
    </div>

    <!-- Empty state -->
    <div v-else class="empty">
      No snapshots yet. Create one in Settings.
    </div>
  </div>
</template>

<style scoped>
.snapshots-page {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.loading {
  padding: 48px 0;
  text-align: center;
  font-size: 13px;
  color: var(--ink-2);
}


/* Header */
.page-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
}
.page-head .page-title { font-size: 22px; }
.page-sub {
  margin: 4px 0 0;
  font-size: 14px;
  color: var(--ink-2);
}

/* Snapshot list — hairline rows, no boxed cards */
.snap-list { display: flex; flex-direction: column; }
.snap-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 14px 4px;
  border-bottom: 1px solid var(--rule-1);
}
.snap-row:last-child { border-bottom: none; }
.snap-text { min-width: 0; }
.snap-date {
  font-size: 13px;
  font-weight: 500;
  color: var(--ink-0);
}
.snap-meta {
  margin-top: 2px;
  font-size: 12px;
  color: var(--ink-2);
}
.snap-dot { margin: 0 5px; color: var(--ink-3); }
.snap-changes { color: var(--accent-text); }
.snap-actions { display: flex; gap: 8px; flex: none; }
.snap-delete { color: var(--ink-2); }
.snap-delete:hover { color: var(--c-again); }

/* Empty state */
.empty {
  padding: 48px 0;
  text-align: center;
  font-size: 13px;
  color: var(--ink-2);
}
</style>
