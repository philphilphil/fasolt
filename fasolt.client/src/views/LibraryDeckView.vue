<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { useLibraryStore } from '@/stores/library'
import { useDecksStore } from '@/stores/decks'
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
const decks = useDecksStore()
const auth = useAuthStore()

const publicId = computed(() => route.params.publicId as string)

const deck = ref<LibraryDeckDetail | null>(null)
const loading = ref(true)
const notFound = ref(false)
const loadError = ref('')

const sampleIndex = ref(0)

/** Which CTA the visitor picked — carried through the logged-out auth funnel. */
type ImportMode = 'copy' | 'link'

const importing = ref<ImportMode | null>(null)
const importError = ref('')
const importedDeck = ref<Deck | null>(null)
const justLinked = ref(false)

/**
 * A linked deck keeps the author's public id, and a deck the visitor owns is in
 * their list under that same id — so the caller's own deck list is all we need
 * to tell owner from subscriber from stranger.
 */
const myDecksLoading = ref(false)
const myDeck = computed(() =>
  auth.isAuthenticated ? decks.decks.find(d => d.id === publicId.value) ?? null : null,
)
const isOwner = computed(() => !!myDeck.value && !myDeck.value.isLinked)
const linkedDeck = computed(() => (myDeck.value?.isLinked ? myDeck.value : null))

const currentSample = computed(() => deck.value?.sampleCards[sampleIndex.value] ?? null)
const publishedLabel = computed(() =>
  deck.value?.publishedAt
    ? new Date(deck.value.publishedAt).toLocaleDateString(undefined, { dateStyle: 'medium' })
    : null,
)

/**
 * Logged-out CTA target. The Razor login/register pages honour a local
 * `returnUrl`, so we bounce through auth and come back to this deck page with
 * `?import=<mode>` — which runs the import the visitor asked for as soon as
 * they are signed in.
 */
function authUrl(path: '/login' | '/register', mode: ImportMode = 'copy') {
  const returnUrl = `/library/${publicId.value}?import=${mode}`
  return `${path}?returnUrl=${encodeURIComponent(returnUrl)}`
}

/** `1` is the pre-link-mode spelling of "copy"; links carrying it may still be in flight. */
function parseImportMode(value: unknown): ImportMode | null {
  if (value === 'link') return 'link'
  if (value === 'copy' || value === '1') return 'copy'
  return null
}

/** The router's afterEach resets the title from route meta, so re-apply ours after navigations. */
function applyTitle() {
  if (deck.value) document.title = formatDocumentTitle(deck.value.name)
}

async function load() {
  loading.value = true
  notFound.value = false
  loadError.value = ''
  sampleIndex.value = 0
  try {
    deck.value = await library.getDeck(publicId.value)
    applyTitle()
  } catch (e) {
    deck.value = null
    // Only a 404 means the deck is really gone. Rate limiting (429), a 5xx or a
    // dropped connection must not tell the visitor a live share link is dead.
    if (isApiError(e) && e.status === 404) notFound.value = true
    else loadError.value = 'Could not load this deck right now. Please try again.'
  } finally {
    loading.value = false
  }
}

/** Owner/subscriber state comes from the caller's own deck list. */
async function loadMyDecks() {
  if (!auth.isAuthenticated) return
  myDecksLoading.value = true
  try {
    await decks.fetchDecks()
  } catch {
    // Leave the CTAs in their default state; the import call reports the real error.
  } finally {
    myDecksLoading.value = false
  }
}

onMounted(async () => {
  const myDecks = loadMyDecks()
  await load()
  const mode = parseImportMode(route.query.import)
  if (mode) {
    // Strip the flag first so a refresh (or the back button) can't import twice.
    const { import: _flag, ...rest } = route.query
    await router.replace({ query: rest })
    applyTitle()
    if (auth.isAuthenticated && deck.value) {
      await (mode === 'link' ? linkDeck() : copyToMyDecks())
    }
  }
  await myDecks
})

watch(publicId, () => {
  importedDeck.value = null
  justLinked.value = false
  importError.value = ''
  load()
})

function showSample(index: number) {
  if (!deck.value) return
  const count = deck.value.sampleCards.length
  if (count === 0) return
  sampleIndex.value = (index + count) % count
}

/** Maps an import failure to a sentence, or bounces to login when the session is gone. */
function reportImportFailure(e: unknown, mode: ImportMode) {
  if (isApiError(e) && e.status === 401) {
    window.location.href = authUrl('/login', mode)
    return
  }
  const fallback = 'Could not import this deck. Please try again.'
  importError.value = isApiError(e)
    ? e.status === 403
      ? 'Verify your email address before importing decks.'
      : e.message || fallback
    : fallback
}

async function copyToMyDecks() {
  if (!deck.value || importing.value) return
  if (!auth.isAuthenticated) {
    window.location.href = authUrl('/register', 'copy')
    return
  }
  importing.value = 'copy'
  importError.value = ''
  justLinked.value = false
  try {
    importedDeck.value = await library.copyDeck(publicId.value)
    // Reflect the new import count without a full refetch round-trip.
    deck.value = { ...deck.value, copyCount: deck.value.copyCount + 1 }
  } catch (e) {
    reportImportFailure(e, 'copy')
  } finally {
    importing.value = null
  }
}

async function linkDeck() {
  if (!deck.value || importing.value) return
  if (!auth.isAuthenticated) {
    window.location.href = authUrl('/register', 'link')
    return
  }
  importing.value = 'link'
  importError.value = ''
  importedDeck.value = null
  try {
    await library.subscribeDeck(publicId.value)
    justLinked.value = true
    // The linked deck now belongs in the caller's list — that list is also what
    // drives the "Linked ✓" state of this CTA.
    await loadMyDecks()
  } catch (e) {
    reportImportFailure(e, 'link')
  } finally {
    importing.value = null
  }
}
</script>

<template>
  <div :class="auth.isAuthenticated ? '' : 'flex min-h-screen flex-col bg-paper-0 text-ink-0'">
    <PublicNav v-if="!auth.isAuthenticated" />

    <!-- Signed in, AppLayout already provides the <main> landmark; nesting a second one is invalid. -->
    <component
      :is="auth.isAuthenticated ? 'div' : 'main'"
      :class="auth.isAuthenticated ? '' : 'mx-auto w-full max-w-5xl flex-1 px-6 py-12'"
    >
      <div class="library-deck-page">
        <RouterLink to="/library" class="fa-back">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m15 18-6-6 6-6"/></svg>
          Deck library
        </RouterLink>

        <div v-if="loading" class="empty">Loading deck…</div>

        <div v-else-if="notFound" class="empty">
          This deck isn't available. It may have been unpublished or deleted.
        </div>

        <div v-else-if="loadError" class="empty">
          <p>{{ loadError }}</p>
          <button type="button" class="fa-btn empty-action" @click="load">Try again</button>
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
            <div v-if="!myDecksLoading" class="deck-actions">
              <template v-if="isOwner">
                <RouterLink :to="`/decks/${publicId}`" class="fa-btn">Manage this deck</RouterLink>
              </template>
              <template v-else-if="auth.isAuthenticated">
                <button
                  type="button"
                  class="fa-btn fa-btn-primary"
                  :disabled="!!importing"
                  @click="copyToMyDecks"
                >
                  {{ importing === 'copy' ? 'Copying…' : 'Copy to my decks' }}
                </button>
                <RouterLink v-if="linkedDeck" :to="`/decks/${publicId}`" class="fa-btn fa-btn-accent">
                  <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="m5 12 5 5L20 7"/></svg>
                  Linked — go to deck
                </RouterLink>
                <button
                  v-else
                  type="button"
                  class="fa-btn"
                  :disabled="!!importing"
                  @click="linkDeck"
                >
                  {{ importing === 'link' ? 'Linking…' : 'Link deck' }}
                </button>
              </template>
              <template v-else>
                <a :href="authUrl('/register', 'copy')" class="fa-btn fa-btn-primary">Copy to my decks</a>
                <a :href="authUrl('/register', 'link')" class="fa-btn">Link deck</a>
                <a :href="authUrl('/login', 'copy')" class="fa-btn fa-btn-ghost">Log in</a>
              </template>
            </div>
          </header>

          <div v-if="isOwner" class="cta-note">This is your deck — you're looking at its public page.</div>
          <div v-else class="cta-note">
            <strong>Link</strong> follows the author's updates and keeps your own study progress.
            <strong>Copy</strong> gives you an independent deck you can edit.
            <template v-if="!auth.isAuthenticated"> Free account required.</template>
          </div>

          <div v-if="importedDeck" class="import-success">
            Imported as a copy — your own cards, your own progress.
            <RouterLink :to="`/decks/${importedDeck.id}`" class="fa-link">Open {{ importedDeck.name }}</RouterLink>
          </div>

          <div v-else-if="justLinked" class="import-success">
            Linked — this deck follows {{ deck.authorHandle ? `@${deck.authorHandle}` : 'the author' }}'s updates.
            <RouterLink :to="`/decks/${publicId}`" class="fa-link">Open {{ deck.name }}</RouterLink>
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

            <LibraryCardPreview v-if="currentSample" :card="currentSample" />
          </section>

          <div v-else class="empty">This deck has no cards to preview yet.</div>
        </template>
      </div>
    </component>

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
  max-width: 560px;
}
.cta-note strong { color: var(--ink-1); font-weight: 600; }

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
.empty p { margin: 0; }
.empty-action { margin-top: 12px; }
</style>
