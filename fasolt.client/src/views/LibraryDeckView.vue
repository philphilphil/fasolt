<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { useLibraryStore } from '@/stores/library'
import { useAuthStore } from '@/stores/auth'
import { isApiError } from '@/api/client'
import { formatDocumentTitle } from '@/router'
import type { Deck, LibraryDeckDetail } from '@/types'
import LibraryCardPreview from '@/components/LibraryCardPreview.vue'
import PublicNav from '@/components/PublicNav.vue'
import AppFooter from '@/components/AppFooter.vue'

const route = useRoute()
const router = useRouter()
const library = useLibraryStore()
const auth = useAuthStore()

const publicId = computed(() => route.params.publicId as string)

const deck = ref<LibraryDeckDetail | null>(null)
const loading = ref(true)
const notFound = ref(false)

const sampleIndex = ref(0)
const isFlipped = ref(false)

const importing = ref(false)
const importError = ref('')
const importedDeck = ref<Deck | null>(null)

const currentSample = computed(() => deck.value?.sampleCards[sampleIndex.value] ?? null)
const publishedLabel = computed(() =>
  deck.value?.publishedAt
    ? new Date(deck.value.publishedAt).toLocaleDateString(undefined, { dateStyle: 'medium' })
    : null,
)

/**
 * Logged-out CTA target. The Razor login/register pages honour a local
 * `returnUrl`, so we bounce through auth and come back to this deck page with
 * `?import=1` — which triggers the copy as soon as the user is signed in.
 */
function authUrl(path: '/login' | '/register') {
  const returnUrl = `/library/${publicId.value}?import=1`
  return `${path}?returnUrl=${encodeURIComponent(returnUrl)}`
}

/** The router's afterEach resets the title from route meta, so re-apply ours after navigations. */
function applyTitle() {
  if (deck.value) document.title = formatDocumentTitle(deck.value.name)
}

async function load() {
  loading.value = true
  notFound.value = false
  sampleIndex.value = 0
  isFlipped.value = false
  try {
    deck.value = await library.getDeck(publicId.value)
    applyTitle()
  } catch {
    deck.value = null
    notFound.value = true
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  await load()
  if (route.query.import === '1') {
    // Strip the flag first so a refresh (or the back button) can't import twice.
    const { import: _flag, ...rest } = route.query
    await router.replace({ query: rest })
    applyTitle()
    if (auth.isAuthenticated && deck.value) await copyToMyDecks()
  }
})

watch(publicId, load)

function showSample(index: number) {
  if (!deck.value) return
  const count = deck.value.sampleCards.length
  if (count === 0) return
  sampleIndex.value = (index + count) % count
  isFlipped.value = false
}

async function copyToMyDecks() {
  if (!deck.value || importing.value) return
  if (!auth.isAuthenticated) {
    window.location.href = authUrl('/register')
    return
  }
  importing.value = true
  importError.value = ''
  try {
    importedDeck.value = await library.copyDeck(publicId.value)
    // Reflect the new import count without a full refetch round-trip.
    deck.value = { ...deck.value, copyCount: deck.value.copyCount + 1 }
  } catch (e) {
    if (isApiError(e) && e.status === 401) {
      window.location.href = authUrl('/login')
      return
    }
    importError.value = isApiError(e)
      ? e.status === 403
        ? 'Verify your email address before importing decks.'
        : e.message || 'Could not import this deck. Please try again.'
      : 'Could not import this deck. Please try again.'
  } finally {
    importing.value = false
  }
}
</script>

<template>
  <div :class="auth.isAuthenticated ? '' : 'flex min-h-screen flex-col bg-paper-0 text-ink-0'">
    <PublicNav v-if="!auth.isAuthenticated" />

    <main :class="auth.isAuthenticated ? '' : 'mx-auto w-full max-w-5xl flex-1 px-6 py-12'">
      <div class="library-deck-page">
        <RouterLink to="/library" class="fa-back">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m15 18-6-6 6-6"/></svg>
          Deck library
        </RouterLink>

        <div v-if="loading" class="empty">Loading deck…</div>

        <div v-else-if="notFound" class="empty">
          This deck isn't available. It may have been unpublished or deleted.
        </div>

        <template v-else-if="deck">
          <header class="deck-header">
            <div class="deck-header-text">
              <h1 class="page-title">{{ deck.name }}</h1>
              <p v-if="deck.description" class="deck-description">{{ deck.description }}</p>
              <div class="deck-facts">
                <span v-if="deck.authorHandle" class="deck-handle">@{{ deck.authorHandle }}</span>
                <span v-if="deck.authorHandle" class="fact-sep">·</span>
                <span><span class="fa-num">{{ deck.cardCount.toLocaleString() }}</span> {{ deck.cardCount === 1 ? 'card' : 'cards' }}</span>
                <template v-if="deck.copyCount > 0">
                  <span class="fact-sep">·</span>
                  <span><span class="fa-num">{{ deck.copyCount.toLocaleString() }}</span> {{ deck.copyCount === 1 ? 'import' : 'imports' }}</span>
                </template>
                <template v-if="publishedLabel">
                  <span class="fact-sep">·</span>
                  <span>Shared {{ publishedLabel }}</span>
                </template>
              </div>
            </div>
            <div class="deck-actions">
              <button
                v-if="auth.isAuthenticated"
                type="button"
                class="fa-btn fa-btn-primary"
                :disabled="importing"
                @click="copyToMyDecks"
              >
                {{ importing ? 'Importing…' : 'Copy to my decks' }}
              </button>
              <template v-else>
                <a :href="authUrl('/register')" class="fa-btn fa-btn-primary">Copy to my decks</a>
                <a :href="authUrl('/login')" class="fa-btn">Log in</a>
              </template>
            </div>
          </header>

          <div v-if="!auth.isAuthenticated" class="cta-note">
            Free account required — sign up and this deck lands in your account, ready to study.
          </div>

          <div v-if="importedDeck" class="import-success">
            Imported as a copy — your own cards, your own progress.
            <RouterLink :to="`/decks/${importedDeck.id}`" class="fa-link">Open {{ importedDeck.name }}</RouterLink>
          </div>

          <div v-if="importError" class="import-error">{{ importError }}</div>

          <div class="fa-rule" />

          <section v-if="deck.sampleCards.length > 0" class="samples-section">
            <div class="samples-head">
              <div class="fa-cap">Sample cards</div>
              <div v-if="deck.sampleCards.length > 1" class="samples-nav">
                <button type="button" class="nav-btn" aria-label="Previous sample card" @click="showSample(sampleIndex - 1)">
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m15 18-6-6 6-6"/></svg>
                </button>
                <span class="samples-count">
                  <span class="fa-num">{{ sampleIndex + 1 }}</span> / <span class="fa-num">{{ deck.sampleCards.length }}</span>
                </span>
                <button type="button" class="nav-btn" aria-label="Next sample card" @click="showSample(sampleIndex + 1)">
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m9 6 6 6-6 6"/></svg>
                </button>
              </div>
            </div>

            <LibraryCardPreview
              v-if="currentSample"
              :card="currentSample"
              :is-flipped="isFlipped"
              @flip="isFlipped = !isFlipped"
            />
          </section>

          <div v-else class="empty">This deck has no cards to preview yet.</div>
        </template>
      </div>
    </main>

    <AppFooter v-if="!auth.isAuthenticated" />
  </div>
</template>

<style scoped>
.library-deck-page {
  padding: 40px 8px 48px;
  max-width: 760px;
  margin: 0 auto;
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: 22px;
}

.deck-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  flex-wrap: wrap;
}
.deck-header-text { min-width: 0; }
.library-deck-page .page-title { font-size: 26px; }
.deck-description {
  margin: 6px 0 0;
  font-size: 14px;
  color: var(--ink-1);
}
.deck-facts {
  display: flex;
  align-items: baseline;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 8px;
  font-size: 12.5px;
  color: var(--ink-2);
}
.deck-handle { color: var(--accent-text); }
.fact-sep { color: var(--ink-3); }
.deck-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.cta-note {
  font-size: 12.5px;
  color: var(--ink-2);
}

.import-success {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  border: 1px solid var(--rule-1);
  background: var(--paper-2);
  border-radius: 9px;
  padding: 10px 14px;
  font-size: 13.5px;
  color: var(--ink-1);
}
.import-error { font-size: 13px; color: var(--c-again); }

.samples-section {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.samples-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}
.samples-nav {
  display: flex;
  align-items: center;
  gap: 8px;
}
.samples-count { font-size: 12.5px; color: var(--ink-2); }
.nav-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  border-radius: 6px;
  border: 1px solid var(--rule-1);
  background: var(--paper-1);
  color: var(--ink-1);
  cursor: pointer;
  transition: background .12s, border-color .12s, color .12s;
}
.nav-btn:hover { background: var(--paper-2); border-color: var(--ink-3); color: var(--ink-0); }

.empty {
  padding: 40px;
  text-align: center;
  color: var(--ink-2);
  font-size: 13px;
}
</style>
