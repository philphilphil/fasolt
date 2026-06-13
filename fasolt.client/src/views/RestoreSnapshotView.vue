<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter, RouterLink } from 'vue-router'
import { useSnapshotsStore } from '@/stores/snapshots'
import { useDecksStore } from '@/stores/decks'
import type { SnapshotDiff, DeckDetail } from '@/types'
import { Checkbox } from '@/components/ui/checkbox'

const route = useRoute()
const router = useRouter()
const snapshotsStore = useSnapshotsStore()
const decksStore = useDecksStore()

const deckId = route.params.id as string
const snapshotId = route.params.snapshotId as string

const deck = ref<DeckDetail | null>(null)
const diff = ref<SnapshotDiff | null>(null)
const loading = ref(true)
const restoring = ref(false)
const error = ref('')
const svgPreview = ref('')

const selected = ref<Record<string, boolean>>({})

onMounted(async () => {
  try {
    const [d, diffResult] = await Promise.all([
      decksStore.getDeckDetail(deckId),
      snapshotsStore.getDiff(snapshotId),
    ])
    deck.value = d
    diff.value = diffResult
    // Deleted cards checked by default
    const init: Record<string, boolean> = {}
    for (const c of diffResult.deleted) init[c.cardId] = true
    for (const c of diffResult.modified) init[c.cardId] = false
    selected.value = init
  } catch {
    error.value = 'Failed to load snapshot diff.'
  } finally {
    loading.value = false
  }
})

const selectedDeletedIds = computed(() => diff.value?.deleted.filter(c => selected.value[c.cardId]).map(c => c.cardId) ?? [])
const selectedModifiedIds = computed(() => diff.value?.modified.filter(c => selected.value[c.cardId]).map(c => c.cardId) ?? [])
const selectedCount = computed(() => selectedDeletedIds.value.length + selectedModifiedIds.value.length)

const isEmpty = computed(() => {
  if (!diff.value) return true
  return diff.value.deleted.length === 0 && diff.value.modified.length === 0 && diff.value.added.length === 0
})

function toggle(cardId: string, checked: boolean) {
  selected.value = { ...selected.value, [cardId]: checked }
}

const selectableIds = computed(() => {
  if (!diff.value) return []
  return [...diff.value.deleted.map(c => c.cardId), ...diff.value.modified.map(c => c.cardId)]
})

const allSelected = computed(() => {
  return selectableIds.value.length > 0 && selectableIds.value.every(id => selected.value[id])
})

function toggleAll() {
  const newVal = !allSelected.value
  const next = { ...selected.value }
  for (const id of selectableIds.value) next[id] = newVal
  selected.value = next
}

async function handleRestore() {
  restoring.value = true
  error.value = ''
  try {
    await snapshotsStore.restore(
      snapshotId,
      selectedDeletedIds.value,
      selectedModifiedIds.value,
    )
    router.push(`/decks/${deckId}`)
  } catch {
    error.value = 'Restore failed. Please try again.'
  } finally {
    restoring.value = false
  }
}

function formatStability(val: number | null) {
  if (val == null) return 'n/a'
  return val.toFixed(1) + 'd'
}

function formatDue(iso: string | null) {
  if (!iso) return 'n/a'
  const d = new Date(iso)
  return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })
}
</script>

<template>
  <div v-if="loading" class="restore-state">Loading…</div>

  <div v-else class="restore-page">
    <RouterLink :to="`/decks/${deckId}/snapshots`" class="fa-back">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m15 18-6-6 6-6"/></svg>
      Snapshots
    </RouterLink>

    <!-- Header -->
    <header class="restore-head">
      <div>
        <h1 class="page-title">Restore snapshot</h1>
        <p class="restore-lede">Select which changes to revert. This restores the selected cards to their snapshot state.</p>
      </div>
      <button class="fa-btn" @click="router.push(`/decks/${deckId}/snapshots`)">
        Back to snapshots
      </button>
    </header>

    <!-- Error -->
    <div v-if="error && !diff" class="restore-state" style="color: var(--c-again)">
      {{ error }}
    </div>

    <!-- Empty diff -->
    <div v-else-if="isEmpty" class="restore-state">
      No differences found. The deck matches this snapshot.
    </div>

    <!-- Diff sections -->
    <div v-else-if="diff" class="diff-sections">
      <!-- Deleted since snapshot -->
      <section v-if="diff.deleted.length > 0">
        <div class="sec-head">
          <span class="sec-dot" style="background: var(--c-again)" />
          <span class="fa-cap">Deleted</span>
          <span class="sec-meta">{{ diff.deleted.length }} card{{ diff.deleted.length !== 1 ? 's' : '' }} removed since snapshot</span>
        </div>
        <div class="rows">
          <label
            v-for="card in diff.deleted"
            :key="card.cardId"
            class="diff-row"
          >
            <Checkbox
              :model-value="!!selected[card.cardId]"
              class="diff-check"
              @update:model-value="toggle(card.cardId, !!$event)"
            />
            <div class="diff-body">
              <div class="diff-front">{{ card.front }}</div>
              <div class="diff-back">{{ card.back }}</div>
              <div class="diff-meta">
                <span :style="{ color: card.stillExists ? 'var(--c-hard)' : 'var(--c-again)' }">{{ card.stillExists ? 'unassigned' : 'deleted' }}</span>
                <span v-if="card.sourceFile">{{ card.sourceFile }}</span>
                <span>stability: {{ formatStability(card.stability) }}</span>
                <span>due: {{ formatDue(card.dueAt) }}</span>
              </div>
            </div>
          </label>
        </div>
      </section>

      <!-- Modified since snapshot -->
      <section v-if="diff.modified.length > 0">
        <div class="sec-head">
          <span class="sec-dot" style="background: var(--c-hard)" />
          <span class="fa-cap">Modified</span>
          <span class="sec-meta">{{ diff.modified.length }} card{{ diff.modified.length !== 1 ? 's' : '' }} changed since snapshot</span>
        </div>
        <div class="rows">
          <label
            v-for="card in diff.modified"
            :key="card.cardId"
            class="diff-row"
          >
            <Checkbox
              :model-value="!!selected[card.cardId]"
              class="diff-check"
              @update:model-value="toggle(card.cardId, !!$event)"
            />
            <div class="diff-body">
              <div v-if="card.front !== card.currentFront || card.back !== card.currentBack" class="diff-change">
                <div v-if="card.front !== card.currentFront">
                  <span class="diff-label">front: </span>
                  <span class="diff-old">{{ card.front }}</span>
                  <span class="diff-new">{{ card.currentFront }}</span>
                </div>
                <div v-if="card.back !== card.currentBack">
                  <span class="diff-label">back: </span>
                  <span class="diff-old">{{ card.back }}</span>
                  <span class="diff-new">{{ card.currentBack }}</span>
                </div>
              </div>
              <div v-else class="diff-front">{{ card.currentFront }}</div>
              <div v-if="card.snapshotFrontSvg !== null || card.currentFrontSvg !== null || card.snapshotBackSvg !== null || card.currentBackSvg !== null" class="diff-meta">
                <span v-if="card.snapshotFrontSvg !== null || card.currentFrontSvg !== null" style="color: var(--c-hard)">front image {{ card.snapshotFrontSvg === null ? 'added' : card.currentFrontSvg === null ? 'removed' : 'changed' }}</span>
                <span v-if="card.snapshotBackSvg !== null || card.currentBackSvg !== null" style="color: var(--c-hard)">back image {{ card.snapshotBackSvg === null ? 'added' : card.currentBackSvg === null ? 'removed' : 'changed' }}</span>
                <button
                  type="button"
                  class="diff-view"
                  @click.prevent="svgPreview = svgPreview === card.cardId ? '' : card.cardId"
                >
                  {{ svgPreview === card.cardId ? 'hide' : 'view' }}
                </button>
              </div>
              <div v-if="svgPreview === card.cardId" class="svg-preview">
                <div v-if="card.snapshotFrontSvg !== null || card.currentFrontSvg !== null" class="svg-pair">
                  <div>
                    <div class="svg-cap">front image — snapshot</div>
                    <div class="svg-frame" v-html="card.snapshotFrontSvg || '(none)'"></div>
                  </div>
                  <div>
                    <div class="svg-cap">front image — current</div>
                    <div class="svg-frame" v-html="card.currentFrontSvg || '(none)'"></div>
                  </div>
                </div>
                <div v-if="card.snapshotBackSvg !== null || card.currentBackSvg !== null" class="svg-pair">
                  <div>
                    <div class="svg-cap">back image — snapshot</div>
                    <div class="svg-frame" v-html="card.snapshotBackSvg || '(none)'"></div>
                  </div>
                  <div>
                    <div class="svg-cap">back image — current</div>
                    <div class="svg-frame" v-html="card.currentBackSvg || '(none)'"></div>
                  </div>
                </div>
              </div>
            </div>
          </label>
        </div>
      </section>

      <!-- Added since snapshot -->
      <section v-if="diff.added.length > 0">
        <div class="sec-head">
          <span class="sec-dot" style="background: var(--c-good)" />
          <span class="fa-cap">Added</span>
          <span class="sec-meta">{{ diff.added.length }} card{{ diff.added.length !== 1 ? 's' : '' }} added since snapshot</span>
        </div>
        <div class="rows">
          <div
            v-for="card in diff.added"
            :key="card.cardId"
            class="diff-row diff-row--static"
          >
            <div class="diff-body">
              <div class="diff-front">{{ card.front }}</div>
              <div class="diff-back">{{ card.back }}</div>
            </div>
          </div>
        </div>
        <p class="rows-note">These cards are unaffected by restore.</p>
      </section>
    </div>

    <!-- Error on restore -->
    <div v-if="error && diff" class="restore-error" style="color: var(--c-again)">{{ error }}</div>

    <!-- Sticky footer -->
    <div v-if="diff && !isEmpty" class="restore-footer">
      <button class="fa-btn fa-btn-ghost" @click="toggleAll">
        {{ allSelected ? 'Deselect all' : 'Select all' }}
      </button>
      <div class="footer-count">
        {{ selectedCount }} card{{ selectedCount !== 1 ? 's' : '' }} selected
      </div>
      <button class="fa-btn" @click="router.push(`/decks/${deckId}/snapshots`)">Cancel</button>
      <button class="fa-btn fa-btn-primary" :disabled="selectedCount === 0 || restoring" @click="handleRestore">
        {{ restoring ? 'Restoring…' : 'Restore selected' }}
      </button>
    </div>
  </div>
</template>

<style scoped>
.restore-page {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.restore-state {
  padding: 48px 0;
  text-align: center;
  font-size: 14px;
  color: var(--ink-2);
}

/* Header */
.restore-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
}
.restore-lede {
  margin: 8px 0 0;
  font-size: 14px;
  color: var(--ink-2);
  max-width: 52ch;
}

/* Diff sections */
.diff-sections { display: flex; flex-direction: column; gap: 28px; }

.sec-head {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}
.sec-dot { width: 7px; height: 7px; border-radius: 999px; flex: none; }
.sec-meta { font-size: 13px; color: var(--ink-2); }

/* Rows — hairline separators, no boxed cards */
.rows { display: flex; flex-direction: column; }
.diff-row {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  padding: 13px 4px;
  border-bottom: 1px solid var(--rule-1);
  cursor: pointer;
}
.diff-row:last-child { border-bottom: none; }
.diff-row--static { cursor: default; }
.diff-check { margin-top: 1px; }
.diff-body { min-width: 0; flex: 1; display: flex; flex-direction: column; gap: 4px; }

.diff-front { font-size: 14px; font-weight: 500; color: var(--ink-0); }
.diff-back { font-size: 14px; color: var(--ink-2); }

.diff-change { display: flex; flex-direction: column; gap: 4px; font-size: 14px; }
.diff-label { color: var(--ink-2); }
.diff-old { color: var(--ink-2); text-decoration: line-through; }
.diff-new { margin-left: 6px; color: var(--ink-0); }

.diff-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 14px;
  font-size: 12px;
  color: var(--ink-2);
}

.diff-view {
  font-size: 12px;
  color: var(--accent-text);
  background: none;
  border: none;
  padding: 0;
  cursor: pointer;
}
.diff-view:hover { color: var(--accent-hi); }

/* SVG preview */
.svg-preview {
  display: flex;
  flex-direction: column;
  gap: 12px;
  margin-top: 8px;
  padding: 12px;
  border: 1px solid var(--rule-1);
  border-radius: 8px;
  background: var(--paper-2);
}
.svg-pair { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.svg-cap { font-size: 12px; color: var(--ink-2); margin-bottom: 4px; }
.svg-frame {
  background: var(--paper-1);
  border: 1px solid var(--rule-1);
  border-radius: 6px;
  padding: 8px;
}

.rows-note { margin: 8px 0 0; font-size: 12px; color: var(--ink-2); }

.restore-error { font-size: 14px; }

/* Sticky footer */
.restore-footer {
  position: sticky;
  bottom: 0;
  display: flex;
  align-items: center;
  gap: 12px;
  margin: 0 -16px;
  padding: 12px 16px;
  background: var(--paper-0);
  border-top: 1px solid var(--rule-1);
}
.footer-count { flex: 1; font-size: 13px; color: var(--ink-2); }
</style>
