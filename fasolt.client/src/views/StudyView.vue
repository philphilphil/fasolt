<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useReviewStore } from '@/stores/review'
import { useDecksStore } from '@/stores/decks'
import { apiFetch } from '@/api/client'
import type { Deck, StudyStats } from '@/types'
import { deckColor } from '@/lib/utils'
import { useKeyboardShortcuts } from '@/composables/useKeyboardShortcuts'

const router = useRouter()
const reviewStore = useReviewStore()
const decksStore = useDecksStore()

const { register, cleanup } = useKeyboardShortcuts()

const totalCards = ref(0)
const dueCount = ref(0)
const studiedToday = ref(0)
const creatingDemo = ref(false)
const studyStats = ref<StudyStats>({ currentStreak: 0, bestStreak: 0, totalAnswered: 0, answeredToday: 0 })

onMounted(async () => {
  try {
    const stats = await reviewStore.fetchStats()
    dueCount.value = stats.dueCount
    totalCards.value = stats.totalCards
    studiedToday.value = stats.studiedToday
  } catch { /* leave as 0 */ }
  try { studyStats.value = await reviewStore.fetchStudyStats() } catch { /* leave at zeros */ }
  decksStore.fetchDecks()

  register({
    Enter: () => { if (dueCount.value > 0) startReview() },
  })
})

onUnmounted(() => { cleanup() })

// Study view focuses on what's actionable: only active decks with cards due
// today. Caught-up or paused decks live in /decks.
const sortedDecks = computed(() => {
  return decksStore.decks
    .filter(d => !d.isSuspended && d.dueCount > 0)
    .sort((a, b) => b.dueCount - a.dueCount || a.name.localeCompare(b.name))
})

const decksWithDue = computed(() => decksStore.decks.filter(d => !d.isSuspended && d.dueCount > 0).length)
const activeDeckCount = computed(() => decksStore.decks.filter(d => !d.isSuspended).length)

// The "Today" star carries meaning: filled/amber once you've reviewed at least
// one card today (streak safe), hollow/muted until then.
const reviewedToday = computed(() => (studyStats.value.answeredToday || 0) > 0)

async function createDemoDeck() {
  creatingDemo.value = true
  try {
    const deck = await apiFetch<Deck>('/demo-deck', { method: 'POST' })
    router.push(`/decks/${deck.id}`)
  } catch { creatingDemo.value = false }
}

function startReview(deckId?: string) {
  if (deckId) router.push(`/review?deckId=${deckId}`)
  else router.push('/review')
}

// Things-style progress ring: how much of a deck is due. r=16 → circumference ≈ 100.53.
const RING_C = 100.53
function ringDash(deck: Deck): string {
  const pct = deck.cardCount ? Math.min(1, deck.dueCount / deck.cardCount) : 0
  return `${(pct * RING_C).toFixed(1)} ${RING_C}`
}
</script>

<template>
  <div class="study-page">
    <!-- Hero -->
    <section class="hero">
      <div
        class="today-row"
        :class="{ 'is-pending': !reviewedToday }"
        :title="reviewedToday ? 'You\'ve reviewed today — your streak is safe' : 'No reviews yet today — review to keep your streak going'"
      >
        <svg width="15" height="15" viewBox="0 0 24 24" :fill="reviewedToday ? 'currentColor' : 'none'" stroke="currentColor" :stroke-width="reviewedToday ? 0 : 1.7" stroke-linejoin="round" aria-hidden="true"><path d="M12 2l2.9 6.3 6.9.6-5.2 4.5 1.6 6.7L12 17.3 5.8 20.6l1.6-6.7L2.2 8.9l6.9-.6z"/></svg>
        Today
      </div>

      <div class="hero-main">
        <span class="hero-number fa-num">{{ dueCount > 0 || totalCards > 0 ? dueCount : 0 }}</span>
        <span class="hero-descriptor">
          <template v-if="dueCount > 0">
            cards due<br />
            <template v-if="decksWithDue > 0">
              across <strong>{{ decksWithDue }} {{ decksWithDue === 1 ? 'deck' : 'decks' }}</strong>
            </template>
            <template v-else>
              across <strong>your library</strong>
            </template>
          </template>
          <template v-else-if="totalCards > 0">
            cards due<br />
            <strong>all caught up</strong>
          </template>
          <template v-else>
            cards yet<br />
            <strong>add your first cards</strong>
          </template>
        </span>
      </div>

      <div class="hero-actions">
        <button
          v-if="dueCount > 0"
          class="fa-btn fa-btn-primary hero-cta"
          @click="startReview()"
        >
          Start reviewing
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12h14M13 5l7 7-7 7"/></svg>
          <span class="cta-kbd">↵</span>
        </button>
        <button
          v-else-if="totalCards === 0"
          class="fa-btn fa-btn-primary hero-cta"
          :disabled="creatingDemo"
          @click="createDemoDeck"
        >
          {{ creatingDemo ? 'Creating…' : 'Create a demo deck' }}
        </button>

        <span v-if="studyStats.currentStreak > 0" class="streak-inline">
          <strong>{{ studyStats.currentStreak }}-day streak</strong> · best {{ studyStats.bestStreak }}
        </span>
        <span v-else-if="totalCards > 0" class="streak-inline">review today to start a streak</span>
      </div>

      <div class="hero-sub-actions">
        <RouterLink to="/decks" class="fa-btn">Browse decks</RouterLink>
        <RouterLink to="/mcp-setup" class="fa-btn fa-btn-ghost">Connect your AI</RouterLink>
      </div>
    </section>

    <!-- Decks with cards due -->
    <section class="decks-section">
      <header class="section-head">
        <h2>Decks with cards due</h2>
      </header>

      <div v-if="decksStore.loading" class="empty">Loading decks…</div>
      <div v-else-if="activeDeckCount === 0" class="empty">
        <span>No decks yet.</span>
        <RouterLink to="/decks" class="fa-link">Create your first deck</RouterLink>
      </div>
      <div v-else-if="sortedDecks.length === 0" class="empty">
        <span>All caught up — nothing due right now.</span>
        <RouterLink to="/decks" class="fa-link">Browse all decks</RouterLink>
      </div>

      <div v-else class="deck-list">
        <button
          v-for="deck in sortedDecks"
          :key="deck.id"
          class="deck-row"
          @click="startReview(deck.id)"
        >
          <svg class="deck-ring" width="22" height="22" viewBox="0 0 36 36" aria-hidden="true">
            <circle cx="18" cy="18" r="16" fill="none" stroke="var(--rule-1)" stroke-width="3" />
            <circle cx="18" cy="18" r="16" fill="none" stroke="var(--accent)" stroke-width="3" stroke-linecap="round" :stroke-dasharray="ringDash(deck)" transform="rotate(-90 18 18)" />
          </svg>
          <span class="fa-tag" :style="{ background: deckColor(deck.id) }" />
          <div class="deck-row-text">
            <div class="deck-row-name">{{ deck.name }}</div>
            <div class="deck-row-sub">{{ deck.cardCount }} cards</div>
          </div>
          <div class="deck-row-due">{{ deck.dueCount }} due</div>
          <svg class="deck-chevron" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m9 6 6 6-6 6"/></svg>
        </button>
      </div>
    </section>

    <!-- Quiet stats line — info without the bordered tile grid -->
    <p v-if="totalCards > 0" class="stats-line">
      <span><strong>{{ studyStats.answeredToday }}</strong> answered today</span>
      <span class="dot">·</span>
      <span><strong>{{ studyStats.totalAnswered.toLocaleString() }}</strong> all time</span>
      <span class="dot">·</span>
      <span><strong>{{ totalCards }}</strong> cards in your library</span>
    </p>
  </div>
</template>

<style scoped>
.study-page {
  display: flex;
  flex-direction: column;
  gap: 40px;
  padding: 40px 8px 48px;
  max-width: 760px;
  margin: 0 auto;
  width: 100%;
}

/* Hero */
.today-row {
  display: flex;
  align-items: center;
  gap: 7px;
  color: var(--accent-text);
  font-size: 14px;
  font-weight: 600;
}
/* Streak not yet kept today — hollow, muted. */
.today-row.is-pending { color: var(--ink-3); }
.hero-main {
  margin: 12px 0 22px;
  display: flex;
  align-items: flex-end;
  gap: 16px;
}
.hero-number {
  font-size: 76px;
  line-height: 0.85;
  letter-spacing: -0.04em;
  color: var(--ink-0);
  font-weight: 600;
}
.hero-descriptor {
  padding-bottom: 8px;
  color: var(--ink-2);
  font-size: 15px;
  line-height: 1.35;
}
.hero-descriptor strong { color: var(--ink-0); font-weight: 600; }
.hero-actions {
  display: flex;
  align-items: center;
  gap: 16px;
  flex-wrap: wrap;
}
.hero-cta {
  height: 40px;
  padding: 0 18px;
  font-size: 14px;
}
.cta-kbd {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  margin-left: 2px;
  width: 18px;
  height: 18px;
  font-size: 11px;
  background: rgba(255,255,255,.18);
  border-radius: 4px;
  color: inherit;
}
.streak-inline { font-size: 13px; color: var(--ink-2); }
.streak-inline strong { color: var(--ink-0); font-weight: 500; }
.hero-sub-actions {
  display: flex;
  gap: 10px;
  margin-top: 16px;
  flex-wrap: wrap;
}
.hero-sub-actions .fa-btn { height: 36px; }

@media (max-width: 600px) {
  .hero-number { font-size: 60px; }
}

/* Section head */
.section-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  margin-bottom: 6px;
}
.section-head h2 {
  font-size: 16px;
  font-weight: 600;
  letter-spacing: -0.01em;
  margin: 0;
  color: var(--ink-1);
}
/* Deck list — rows on the page, hairline separators, no outer box */
.deck-list { display: flex; flex-direction: column; }
.deck-row {
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
.deck-row:last-child { border-bottom: none; }
.deck-row:hover { background: var(--paper-2); }
.deck-ring { flex: none; }
.deck-row-text { flex: 1; min-width: 0; }
.deck-row-name {
  font-size: 15.5px;
  color: var(--ink-0);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.deck-row-sub { font-size: 12.5px; color: var(--ink-2); margin-top: 1px; }
.deck-row-due { font-size: 14px; color: var(--accent-text); font-weight: 600; }
.deck-chevron { color: var(--ink-3); flex: none; }

/* Quiet stats line */
.stats-line {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  font-size: 13px;
  color: var(--ink-2);
  margin: 0;
}
.stats-line strong { color: var(--ink-0); font-weight: 600; }
.stats-line .dot { color: var(--ink-3); }

/* Empty state */
.empty {
  padding: 24px 0;
  color: var(--ink-2);
  font-size: 13.5px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}
</style>
