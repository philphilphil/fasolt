<script setup lang="ts">
import { computed, onMounted, ref, type Component } from 'vue'
import { RouterLink } from 'vue-router'
import { Check, Copy, Globe, Link2, Lock } from 'lucide-vue-next'
import { useDecksStore } from '@/stores/decks'
import { useAuthStore } from '@/stores/auth'
import { isApiError } from '@/api/client'
import type { Deck, DeckVisibility } from '@/types'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/components/ui/dialog'

const props = defineProps<{
  deckId: string
  visibility: DeckVisibility
  copyCount: number
}>()

const emit = defineEmits<{ updated: [deck: Deck] }>()

const decks = useDecksStore()
const auth = useAuthStore()

const busy = ref(false)
const error = ref('')

const handleOpen = ref(false)
const handleInput = ref('')
const handleError = ref('')
const handleSaving = ref(false)
// Visibility to apply once a handle has been claimed (the first-publish flow).
const pendingVisibility = ref<DeckVisibility | null>(null)

const OPTIONS: { value: DeckVisibility; label: string; hint: string; Icon: Component }[] = [
  { value: 'private', label: 'Private', hint: 'Only you can see this deck.', Icon: Lock },
  { value: 'unlisted', label: 'Unlisted', hint: 'Anyone with the link can view and import it. Not listed in the library.', Icon: Link2 },
  { value: 'public', label: 'Public', hint: 'Listed in the public deck library for anyone to find and import.', Icon: Globe },
]

const shareUrl = computed(() => `${window.location.origin}/library/${props.deckId}`)
const isShared = computed(() => props.visibility !== 'private')
const canPublish = computed(() => auth.handle?.canPublish ?? true)
const urlCopied = ref(false)

onMounted(() => {
  // The share UI needs to know whether a handle exists before the user picks
  // Public, so the prompt can come first instead of as a rejected request.
  auth.fetchHandle().catch(() => { /* handle stays unknown; publish reports it */ })
})

async function applyVisibility(visibility: DeckVisibility) {
  if (busy.value || visibility === props.visibility) return
  error.value = ''

  if (visibility === 'public' && auth.handle && !auth.handle.handle) {
    promptForHandle(visibility)
    return
  }

  busy.value = true
  try {
    emit('updated', await decks.setVisibility(props.deckId, visibility))
  } catch (e) {
    if (isApiError(e) && e.code === 'handle_required') {
      promptForHandle(visibility)
    } else {
      error.value = isApiError(e) && e.message
        ? e.message
        : 'Could not change the visibility of this deck. Please try again.'
    }
  } finally {
    busy.value = false
  }
}

function promptForHandle(visibility: DeckVisibility) {
  pendingVisibility.value = visibility
  handleInput.value = auth.handle?.handle ?? ''
  handleError.value = ''
  handleOpen.value = true
}

async function saveHandle() {
  const value = handleInput.value.trim()
  if (!value || handleSaving.value) return
  handleSaving.value = true
  handleError.value = ''
  try {
    await auth.setHandle(value)
    handleOpen.value = false
    const next = pendingVisibility.value
    pendingVisibility.value = null
    if (next) await applyVisibility(next)
  } catch (e) {
    handleError.value = isApiError(e) && e.message
      ? e.message
      : 'Could not save that handle. Please try again.'
  } finally {
    handleSaving.value = false
  }
}

async function copyShareUrl() {
  await navigator.clipboard.writeText(shareUrl.value)
  urlCopied.value = true
  setTimeout(() => urlCopied.value = false, 2000)
}
</script>

<template>
  <section class="share-section">
    <div class="fa-cap section-label">Sharing</div>

    <div class="visibility-options">
      <button
        v-for="option in OPTIONS"
        :key="option.value"
        type="button"
        class="visibility-option"
        :class="{ 'is-active': visibility === option.value }"
        :aria-pressed="visibility === option.value"
        :disabled="busy || (option.value === 'public' && !canPublish)"
        @click="applyVisibility(option.value)"
      >
        <component :is="option.Icon" :size="15" class="option-icon" />
        <span class="option-text">
          <span class="option-label">{{ option.label }}</span>
          <span class="option-hint">{{ option.hint }}</span>
        </span>
        <Check v-if="visibility === option.value" :size="15" class="option-check" />
      </button>
    </div>

    <p v-if="!canPublish" class="share-note">
      Publishing to the library is disabled for your account. You can still share an unlisted link.
    </p>

    <div v-if="error" class="share-error">{{ error }}</div>

    <template v-if="isShared">
      <div class="share-url">
        <span class="share-url-text fa-mono">{{ shareUrl }}</span>
        <button type="button" class="fa-btn share-copy" @click="copyShareUrl">
          <Check v-if="urlCopied" :size="13" />
          <Copy v-else :size="13" />
          {{ urlCopied ? 'Copied!' : 'Copy link' }}
        </button>
      </div>
      <p class="share-note">
        <template v-if="copyCount > 0">
          Imported <span class="fa-num">{{ copyCount }}</span> {{ copyCount === 1 ? 'time' : 'times' }} ·
        </template>
        <RouterLink :to="`/library/${deckId}`" class="fa-link">View the public page</RouterLink>
      </p>
    </template>

    <Dialog v-model:open="handleOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Choose your handle</DialogTitle>
          <DialogDescription>
            Public decks are credited to a handle. Pick one — 3–30 characters, lowercase
            letters, numbers and hyphens.
          </DialogDescription>
        </DialogHeader>
        <div class="handle-field">
          <span class="handle-at">@</span>
          <Input
            v-model="handleInput"
            placeholder="your-handle"
            autocomplete="off"
            @keydown.enter="saveHandle"
          />
        </div>
        <div v-if="handleError" class="share-error">{{ handleError }}</div>
        <DialogFooter class="gap-2">
          <Button variant="outline" size="sm" :disabled="handleSaving" @click="handleOpen = false">Cancel</Button>
          <Button size="sm" :disabled="handleSaving || !handleInput.trim()" @click="saveHandle">
            {{ handleSaving ? 'Saving…' : 'Save handle' }}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </section>
</template>

<style scoped>
.share-section {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.section-label { margin-bottom: 2px; }

.visibility-options {
  display: flex;
  flex-direction: column;
  border: 1px solid var(--rule-1);
  border-radius: 10px;
  overflow: hidden;
  max-width: 560px;
}
.visibility-option {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  padding: 11px 13px;
  background: var(--paper-1);
  border: none;
  border-bottom: 1px solid var(--rule-1);
  text-align: left;
  font: inherit;
  color: var(--ink-0);
  cursor: pointer;
  transition: background .12s;
}
.visibility-option:last-child { border-bottom: none; }
.visibility-option:hover:not(:disabled) { background: var(--paper-2); }
.visibility-option:disabled { opacity: 0.5; cursor: not-allowed; }
.visibility-option.is-active { background: var(--accent-soft); }
.option-icon { flex: none; margin-top: 2px; color: var(--ink-2); }
.visibility-option.is-active .option-icon { color: var(--accent-text); }
.option-text { display: flex; flex-direction: column; min-width: 0; flex: 1; }
.option-label { font-size: 13.5px; font-weight: 500; }
.option-hint { font-size: 12px; color: var(--ink-2); margin-top: 1px; }
.option-check { flex: none; margin-top: 2px; color: var(--accent-text); }

.share-url {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
  max-width: 560px;
}
.share-url-text {
  flex: 1;
  min-width: 0;
  padding: 6px 10px;
  border: 1px solid var(--rule-1);
  border-radius: 8px;
  background: var(--paper-2);
  font-size: 12.5px;
  color: var(--ink-1);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.share-copy { flex: none; height: 30px; }

.share-note { margin: 0; font-size: 12.5px; color: var(--ink-2); }
.share-error { font-size: 13px; color: var(--c-again); }

.handle-field {
  display: flex;
  align-items: center;
  gap: 8px;
}
.handle-at { font-size: 15px; color: var(--ink-2); }
</style>
