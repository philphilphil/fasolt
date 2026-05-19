<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useDecksStore } from '@/stores/decks'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogTrigger,
} from '@/components/ui/dialog'
import { deckColor } from '@/lib/utils'

const router = useRouter()
const decks = useDecksStore()
const newName = ref('')
const newDescription = ref('')
const dialogOpen = ref(false)
const createError = ref('')

onMounted(() => decks.fetchDecks())

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
</script>

<template>
  <div class="decks-page">
    <header class="decks-head">
      <h1 class="page-title">Decks</h1>
      <div class="head-actions">
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

    <div v-else class="deck-grid">
      <article
        v-for="deck in sortedDecks"
        :key="deck.id"
        class="deck-card"
        :class="{ 'is-suspended': deck.isSuspended }"
      >
        <span class="deck-stripe" :style="{ background: deckColor(deck.id) }" aria-hidden="true" />
        <header class="deck-card-head">
          <div class="deck-card-title">
            <span class="fa-tag" :style="{ background: deckColor(deck.id) }" aria-hidden="true" />
            <h3>
              <RouterLink :to="`/decks/${deck.id}`" class="deck-card-link">{{ deck.name }}</RouterLink>
            </h3>
          </div>
          <span v-if="deck.isSuspended" class="paused-chip fa-cap">paused</span>
        </header>
        <p class="deck-card-desc">
          {{ deck.description || 'No description yet — ask your AI to add one.' }}
        </p>
        <footer class="deck-card-foot">
          <div class="deck-card-counts">
            <div class="count-block">
              <span class="fa-mono count-num">{{ deck.cardCount }}</span>
              <span class="fa-cap count-label">cards</span>
            </div>
            <div class="count-block">
              <span class="fa-mono count-num" :class="{ 'is-due': deck.dueCount > 0 }">{{ deck.dueCount }}</span>
              <span class="fa-cap count-label">due</span>
            </div>
          </div>
          <div v-if="!deck.isSuspended && deck.cardCount > 0" class="deck-card-actions">
            <button
              v-if="deck.dueCount > 0"
              type="button"
              class="fa-btn fa-btn-accent deck-card-cta"
              :aria-label="`Review ${deck.name}`"
              @click="startReview(deck.id)"
            >
              Review
              <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12h14M13 5l7 7-7 7"/></svg>
            </button>
            <button
              type="button"
              class="deck-card-cram"
              :aria-label="`Custom study for ${deck.name}`"
              @click="startCustomStudy(deck.id)"
            >
              Custom
            </button>
          </div>
          <span v-else class="fa-cap deck-card-status">
            {{ deck.isSuspended ? 'resume' : 'empty' }}
          </span>
        </footer>
      </article>

      <button type="button" class="new-deck-card fa-stripe-bg" @click="dialogOpen = true">
        <div class="plus-tile">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14M5 12h14"/></svg>
        </div>
        <div class="new-deck-label">New deck</div>
        <div class="new-deck-hint">or ask your AI: "make a deck from notes/biology.md"</div>
      </button>
    </div>

    <div v-if="!decks.loading && decks.decks.length === 0" class="empty">
      No decks yet. Create one to organize your cards.
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
.meta-mcp {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 11.5px;
  color: var(--ink-2);
}

.deck-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 14px;
}
@media (max-width: 1000px) { .deck-grid { grid-template-columns: repeat(2, 1fr); } }
@media (max-width: 640px) { .deck-grid { grid-template-columns: 1fr; } }

.deck-card {
  position: relative;
  display: flex;
  flex-direction: column;
  gap: 10px;
  min-height: 196px;
  padding: 16px 18px 14px;
  border: 1px solid var(--rule-1);
  border-radius: 12px;
  background: var(--paper-1);
  text-align: left;
  font: inherit;
  color: inherit;
  transition: border-color .12s, transform .08s, box-shadow .12s;
}
.deck-card:has(.deck-card-link:hover),
.deck-card:has(.deck-card-link:focus-visible) {
  border-color: var(--ink-3);
  transform: translateY(-1px);
  box-shadow: var(--sh-2);
}
.deck-card.is-suspended { opacity: 0.7; }

.deck-stripe {
  position: absolute;
  top: 0;
  left: 16px;
  right: 16px;
  height: 3px;
  border-radius: 0 0 2px 2px;
}

.deck-card-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 10px;
  margin-top: 6px;
}
.deck-card-title {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}
.deck-card-title h3 {
  font-size: 15px;
  font-weight: 600;
  color: var(--ink-0);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  margin: 0;
}
.deck-card-link {
  color: inherit;
  text-decoration: none;
  outline: none;
}
.deck-card-link::after {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: 12px;
  z-index: 0;
  cursor: pointer;
}
.deck-card-link:focus-visible::after {
  box-shadow: 0 0 0 2px var(--accent);
}
.paused-chip {
  font-size: 9px;
  color: var(--c-hard);
  padding: 2px 6px;
  border: 1px solid var(--c-hard);
  border-radius: 4px;
}
.deck-card-desc {
  font-size: 12.5px;
  color: var(--ink-2);
  line-height: 1.4;
  min-height: 36px;
  margin: 0;
}
.deck-card-foot {
  margin-top: auto;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding-top: 12px;
  border-top: 1px solid var(--rule-1);
}
.deck-card-counts {
  display: flex;
  gap: 14px;
  align-items: baseline;
}
.count-block {
  display: flex;
  align-items: baseline;
  gap: 4px;
}
.count-num {
  font-size: 18px;
  font-weight: 600;
  color: var(--ink-0);
}
.count-num.is-due { color: var(--accent); }
.count-label { font-size: 9px; }
.deck-card-actions {
  display: flex;
  align-items: center;
  gap: 6px;
  position: relative;
  z-index: 1;
}
.deck-card-cta {
  height: 26px;
  padding: 0 10px;
  font-size: 11.5px;
}
.deck-card-cram {
  height: 26px;
  padding: 0 10px;
  border-radius: 6px;
  border: 1px solid transparent;
  background: transparent;
  color: var(--ink-2);
  font: inherit;
  font-size: 11.5px;
  font-weight: 500;
  cursor: pointer;
  transition: color .12s, background .12s, border-color .12s;
}
.deck-card-cram:hover {
  color: var(--ink-0);
  background: var(--paper-2);
  border-color: var(--rule-1);
}
.deck-card-status {
  font-size: 9px;
  color: var(--ink-2);
}

.new-deck-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 10px;
  min-height: 196px;
  border: 1px dashed var(--rule-1);
  border-radius: 12px;
  color: var(--ink-2);
  background-color: transparent;
  cursor: pointer;
  transition: border-color .12s, color .12s;
  font: inherit;
}
.new-deck-card:hover {
  border-color: var(--accent);
  color: var(--accent);
}
.plus-tile {
  width: 36px;
  height: 36px;
  border-radius: 8px;
  border: 1px solid currentColor;
  display: grid;
  place-items: center;
}
.new-deck-label {
  font-size: 13px;
  font-weight: 500;
}
.new-deck-hint {
  font-size: 11px;
  color: var(--ink-3);
  text-align: center;
  max-width: 220px;
  line-height: 1.4;
}

.empty {
  padding: 40px;
  text-align: center;
  color: var(--ink-2);
  font-size: 13px;
}
</style>
