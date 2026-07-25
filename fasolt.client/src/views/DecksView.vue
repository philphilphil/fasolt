<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useDecksStore } from '@/stores/decks'
import { useSnapshotsStore } from '@/stores/snapshots'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogTrigger,
} from '@/components/ui/dialog'
import { deckColor } from '@/lib/utils'

const router = useRouter()
const decks = useDecksStore()
const snapshotsStore = useSnapshotsStore()
const newName = ref('')
const newDescription = ref('')
const dialogOpen = ref(false)
const createError = ref('')

const snapshotDialogOpen = ref(false)
const snapshotting = ref(false)
const snapshotSuccess = ref(false)
const snapshotError = ref('')
const snapshotCreated = ref(0)
const snapshotSkipped = ref(0)

const snapshotCount = computed(() => snapshotsStore.snapshots.length)
const lastSnapshot = computed(() => {
  if (snapshotsStore.snapshots.length === 0) return null
  const date = new Date(snapshotsStore.snapshots[0].createdAt)
  return date.toLocaleDateString(undefined, { dateStyle: 'medium' })
})
const totalCardsBacked = computed(() =>
  snapshotsStore.snapshots.reduce((sum, s) => sum + s.cardCount, 0),
)

async function createSnapshot() {
  snapshotting.value = true
  snapshotSuccess.value = false
  snapshotError.value = ''
  try {
    const result = await snapshotsStore.createAll()
    snapshotCreated.value = result.created
    snapshotSkipped.value = result.skipped
    snapshotSuccess.value = true
    snapshotsStore.fetchRecent()
  } catch {
    snapshotError.value = 'Failed to create snapshot. Please try again.'
  } finally {
    snapshotting.value = false
  }
}

onMounted(() => {
  decks.fetchDecks()
  snapshotsStore.fetchRecent()
})

const sortedDecks = computed(() =>
  [...decks.decks].sort((a, b) => {
    if (a.isSuspended !== b.isSuspended) return a.isSuspended ? 1 : -1
    if ((b.dueCount > 0) !== (a.dueCount > 0)) return b.dueCount - a.dueCount
    return a.name.localeCompare(b.name)
  })
)

const totalCards = computed(() => decks.decks.reduce((a, d) => a + d.cardCount, 0))
const totalDue = computed(() => decks.decks.reduce((a, d) => a + (d.isSuspended ? 0 : d.dueCount), 0))

async function createDeck() {
  const name = newName.value.trim()
  if (!name) return
  createError.value = ''
  try {
    await decks.createDeck(name, newDescription.value.trim() || undefined)
    newName.value = ''
    newDescription.value = ''
    dialogOpen.value = false
  } catch {
    createError.value = 'Failed to create deck. Please try again.'
  }
}

function startReview(id: string) { router.push(`/review?deckId=${id}`) }
function startCustomStudy(id: string) { router.push(`/review?deckId=${id}&mode=cram`) }

// Things-style progress ring: how much of a deck is due. r=16 → circumference ≈ 100.53.
const RING_C = 100.53
function ringDash(deck: { cardCount: number; dueCount: number }): string {
  const pct = deck.cardCount ? Math.min(1, deck.dueCount / deck.cardCount) : 0
  return `${(pct * RING_C).toFixed(1)} ${RING_C}`
}
</script>

<template>
  <div class="decks-page">
    <header class="decks-head">
      <h1 class="page-title">Decks</h1>
      <div class="head-actions">
        <Dialog v-model:open="snapshotDialogOpen">
          <DialogTrigger as-child>
            <button class="fa-btn" type="button">
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M3 12a9 9 0 1 0 3-6.7L3 8"/><path d="M3 3v5h5"/></svg>
              Snapshots
            </button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Snapshots</DialogTitle>
            </DialogHeader>
            <div class="flex flex-col gap-3">
              <p class="text-sm text-muted-foreground">
                Back up every card's content so you can restore it later if something goes wrong. The last 10 snapshots per deck are kept automatically. Restoring only reverts card content — your study progress is never touched.
              </p>
              <div v-if="snapshotSuccess" class="rounded border border-success/20 bg-success/10 px-3 py-2 text-sm text-success">
                <template v-if="snapshotCreated > 0 && snapshotSkipped === 0">
                  Snapshot created for {{ snapshotCreated }} deck{{ snapshotCreated !== 1 ? 's' : '' }}.
                </template>
                <template v-else-if="snapshotCreated > 0 && snapshotSkipped > 0">
                  Snapshot created for {{ snapshotCreated }} deck{{ snapshotCreated !== 1 ? 's' : '' }}. {{ snapshotSkipped }} unchanged, skipped.
                </template>
                <template v-else>
                  All decks unchanged — no snapshots needed.
                </template>
              </div>
              <div v-if="snapshotError" class="rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ snapshotError }}</div>
              <div v-if="snapshotCount > 0" class="flex gap-6 text-sm">
                <div class="flex flex-col">
                  <span class="text-muted-foreground">Snapshots</span>
                  <span class="font-medium">{{ snapshotCount }}</span>
                </div>
                <div class="flex flex-col">
                  <span class="text-muted-foreground">Cards backed up</span>
                  <span class="font-medium">{{ totalCardsBacked }}</span>
                </div>
                <div v-if="lastSnapshot" class="flex flex-col">
                  <span class="text-muted-foreground">Last snapshot</span>
                  <span class="font-medium">{{ lastSnapshot }}</span>
                </div>
              </div>
            </div>
            <DialogFooter>
              <Button :disabled="snapshotting" @click="createSnapshot">
                {{ snapshotting ? 'Creating…' : 'Create snapshot' }}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
        <Dialog v-model:open="dialogOpen">
          <DialogTrigger as-child>
            <button class="fa-btn fa-btn-primary">
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14M5 12h14"/></svg>
              New deck
            </button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Create deck</DialogTitle>
            </DialogHeader>
            <div class="space-y-3">
              <Input v-model="newName" placeholder="Deck name" @keydown.enter="createDeck" />
              <Input v-model="newDescription" placeholder="Description (optional)" @keydown.enter="createDeck" />
            </div>
            <div v-if="createError" class="text-xs text-destructive">{{ createError }}</div>
            <DialogFooter>
              <Button @click="createDeck">Create</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>
    </header>

    <div class="decks-meta">
      <span class="meta-text">
        <strong>{{ decks.decks.length }} {{ decks.decks.length === 1 ? 'deck' : 'decks' }}</strong>
        · {{ totalCards.toLocaleString() }} total cards · {{ totalDue }} due
      </span>
    </div>

    <div v-if="decks.loading" class="empty">Loading decks…</div>

    <div v-else-if="decks.decks.length === 0" class="empty">
      No decks yet. Create one to organize your cards.
    </div>

    <div v-else class="deck-list">
      <div
        v-for="deck in sortedDecks"
        :key="deck.id"
        class="deck-row"
        :class="{ 'is-suspended': deck.isSuspended }"
      >
        <RouterLink :to="`/decks/${deck.id}`" class="deck-row-main" :aria-label="`Open ${deck.name}`">
          <svg
            v-if="!deck.isSuspended && deck.cardCount > 0"
            class="deck-ring" width="22" height="22" viewBox="0 0 36 36" aria-hidden="true"
          >
            <circle cx="18" cy="18" r="16" fill="none" stroke="var(--rule-1)" stroke-width="3" />
            <circle cx="18" cy="18" r="16" fill="none" stroke="var(--accent)" stroke-width="3" stroke-linecap="round" :stroke-dasharray="ringDash(deck)" transform="rotate(-90 18 18)" />
          </svg>
          <span v-else class="deck-ring-spacer" aria-hidden="true" />
          <span class="fa-tag" :style="{ background: deckColor(deck.id) }" aria-hidden="true" />
          <div class="deck-row-text">
            <div class="deck-row-name-line">
              <span class="deck-row-name">{{ deck.name }}</span>
              <span v-if="deck.isLinked" class="linked-chip">Linked</span>
              <span v-if="deck.isSuspended" class="paused-chip">Paused</span>
            </div>
            <div class="deck-row-sub">
              <span v-if="deck.isLinked && deck.authorHandle" class="deck-row-handle">@{{ deck.authorHandle }}</span>
              <span v-if="deck.isLinked && deck.authorHandle" class="deck-row-sep">·</span>
              {{ deck.description || `${deck.cardCount} ${deck.cardCount === 1 ? 'card' : 'cards'}` }}
            </div>
          </div>
        </RouterLink>

        <div class="deck-row-trail">
          <span v-if="deck.dueCount > 0 && !deck.isSuspended" class="deck-row-due">{{ deck.dueCount }} due</span>
          <span v-else-if="deck.isSuspended" class="deck-row-meta">{{ deck.cardCount }} cards</span>
          <span v-else class="deck-row-meta">All caught up</span>

          <div v-if="!deck.isSuspended && deck.cardCount > 0" class="deck-row-actions">
            <button
              v-if="deck.dueCount > 0"
              type="button"
              class="fa-btn fa-btn-accent deck-row-cta"
              :aria-label="`Review ${deck.name}`"
              @click="startReview(deck.id)"
            >
              Review
              <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12h14M13 5l7 7-7 7"/></svg>
            </button>
            <button
              type="button"
              class="deck-row-cram"
              :aria-label="`Custom study for ${deck.name}`"
              @click="startCustomStudy(deck.id)"
            >
              Custom
            </button>
          </div>

          <RouterLink :to="`/decks/${deck.id}`" class="deck-chevron-link" tabindex="-1" aria-hidden="true">
            <svg class="deck-chevron" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m9 6 6 6-6 6"/></svg>
          </RouterLink>
        </div>
      </div>

      <button type="button" class="new-deck-row" @click="dialogOpen = true">
        <span class="plus-tile">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14M5 12h14"/></svg>
        </span>
        <span class="new-deck-text">
          <span class="new-deck-label">New deck</span>
          <span class="new-deck-hint">or ask your AI: "make a deck from notes/biology.md"</span>
        </span>
      </button>
    </div>
  </div>
</template>

<style scoped>
.decks-page {
  padding: 28px 0 40px;
  display: flex;
  flex-direction: column;
  gap: 22px;
}
.decks-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 16px;
}
.head-actions { display: flex; gap: 8px; }
.decks-meta {
  display: flex;
  align-items: center;
  gap: 18px;
  flex-wrap: wrap;
}
.meta-text { font-size: 13px; color: var(--ink-1); }
.meta-text strong { color: var(--ink-0); font-weight: 600; }

/* Deck list — rows on the page, hairline separators, no outer box */
.deck-list { display: flex; flex-direction: column; }

.deck-row {
  display: flex;
  align-items: center;
  gap: 12px;
  border-bottom: 1px solid var(--rule-1);
  transition: background .12s;
}
.deck-row:hover { background: var(--paper-2); }
.deck-row.is-suspended { opacity: 0.65; }

.deck-row-main {
  flex: 1;
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 15px 4px;
  text-decoration: none;
  color: inherit;
  border-radius: 8px;
  outline: none;
}
.deck-row-main:focus-visible { box-shadow: 0 0 0 2px var(--accent); }
.deck-ring { flex: none; }
.deck-ring-spacer { width: 22px; height: 22px; flex: none; }

.deck-row-text { flex: 1; min-width: 0; }
.deck-row-name-line {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}
.deck-row-name {
  font-size: 15.5px;
  color: var(--ink-0);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.paused-chip {
  flex: none;
  font-size: 11px;
  font-weight: 500;
  color: var(--ink-2);
  padding: 1px 7px;
  border: 1px solid var(--rule-1);
  border-radius: 999px;
}
.linked-chip {
  flex: none;
  font-size: 11px;
  font-weight: 500;
  color: var(--accent-text);
  padding: 1px 7px;
  border: 1px solid var(--accent);
  border-radius: 999px;
}
.deck-row-handle { color: var(--accent-text); }
.deck-row-sep { color: var(--ink-3); margin: 0 4px; }
.deck-row-sub {
  font-size: 12.5px;
  color: var(--ink-2);
  margin-top: 2px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.deck-row-trail {
  display: flex;
  align-items: center;
  gap: 12px;
  padding-right: 2px;
  flex: none;
}
.deck-row-due {
  font-size: 14px;
  color: var(--accent-text);
  font-weight: 600;
  white-space: nowrap;
}
.deck-row-meta {
  font-size: 13px;
  color: var(--ink-2);
  white-space: nowrap;
}
.deck-row-actions {
  display: flex;
  align-items: center;
  gap: 6px;
}
.deck-row-cta {
  height: 28px;
  padding: 0 11px;
  font-size: 12px;
}
.deck-row-cram {
  height: 28px;
  padding: 0 10px;
  border-radius: 7px;
  border: 1px solid transparent;
  background: transparent;
  color: var(--ink-2);
  font: inherit;
  font-size: 12px;
  font-weight: 500;
  cursor: pointer;
  transition: color .12s, background .12s, border-color .12s;
}
.deck-row-cram:hover {
  color: var(--ink-0);
  background: var(--paper-2);
  border-color: var(--rule-1);
}
.deck-chevron-link {
  display: inline-flex;
  color: var(--ink-3);
}
.deck-chevron { flex: none; }

/* New deck — quiet row, no dashed box / accent hover */
.new-deck-row {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 15px 4px;
  width: 100%;
  text-align: left;
  background: transparent;
  border: none;
  cursor: pointer;
  color: var(--ink-1);
  font: inherit;
  border-radius: 8px;
  transition: background .12s;
}
.new-deck-row:hover { background: var(--paper-2); }
.plus-tile {
  width: 22px;
  height: 22px;
  flex: none;
  border-radius: 6px;
  border: 1px solid var(--rule-1);
  color: var(--ink-2);
  display: grid;
  place-items: center;
}
.new-deck-text { display: flex; flex-direction: column; min-width: 0; }
.new-deck-label {
  font-size: 14.5px;
  font-weight: 500;
  color: var(--ink-0);
}
.new-deck-hint {
  font-size: 12px;
  color: var(--ink-2);
  margin-top: 2px;
}

.empty {
  padding: 40px;
  text-align: center;
  color: var(--ink-2);
  font-size: 13px;
}

@media (max-width: 560px) {
  .deck-row-cram { display: none; }
}
</style>
