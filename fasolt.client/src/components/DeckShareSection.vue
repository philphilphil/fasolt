<script setup lang="ts">
import { computed, onMounted, ref, type Component } from 'vue'
import { RouterLink } from 'vue-router'
import { Check, ChevronDown, Copy, Globe, Link2, Lock, Share2 } from 'lucide-vue-next'
import { useDecksStore } from '@/stores/decks'
import { useAuthStore } from '@/stores/auth'
import { isApiError } from '@/api/client'
import type { Deck, DeckVisibility } from '@/types'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/components/ui/dialog'
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator, DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'

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

const activeOption = computed(() => OPTIONS.find(o => o.value === props.visibility) ?? OPTIONS[0])
const shareUrl = computed(() => `${window.location.origin}/library/${props.deckId}`)
const isShared = computed(() => props.visibility !== 'private')
const canPublish = computed(() => auth.handle?.canPublish ?? true)
const urlCopied = ref(false)

// Trigger reflects current state: a plain "Share" prompt, or the active
// visibility once the deck is shared.
const triggerIcon = computed(() => (isShared.value ? activeOption.value.Icon : Share2))
const triggerLabel = computed(() => (isShared.value ? 'Shared' : 'Share'))

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
  <DropdownMenu>
    <DropdownMenuTrigger as-child>
      <button type="button" class="fa-btn share-trigger" :disabled="busy">
        <component :is="triggerIcon" :size="14" />
        {{ triggerLabel }}
        <ChevronDown :size="13" class="share-trigger-chevron" />
      </button>
    </DropdownMenuTrigger>
    <DropdownMenuContent align="start" class="w-72">
      <DropdownMenuItem
        v-for="option in OPTIONS"
        :key="option.value"
        role="menuitemradio"
        :aria-checked="visibility === option.value"
        :disabled="option.value === 'public' && !canPublish"
        class="share-option cursor-pointer"
        @click="applyVisibility(option.value)"
      >
        <component :is="option.Icon" :size="15" class="share-option-icon" />
        <span class="share-option-text">
          <span class="share-option-label">{{ option.label }}</span>
          <span class="share-option-hint">{{ option.hint }}</span>
        </span>
        <Check v-if="visibility === option.value" :size="15" class="share-option-check" />
      </DropdownMenuItem>

      <template v-if="isShared">
        <DropdownMenuSeparator />
        <DropdownMenuItem class="share-url-item cursor-pointer" @select.prevent="copyShareUrl">
          <span class="share-url-text fa-mono">{{ shareUrl }}</span>
          <span class="share-copy-action">
            <Check v-if="urlCopied" :size="13" />
            <Copy v-else :size="13" />
            {{ urlCopied ? 'Copied!' : 'Copy link' }}
          </span>
        </DropdownMenuItem>
        <p class="share-note share-note-menu">
          <template v-if="copyCount > 0">
            Imported <span class="fa-num">{{ copyCount }}</span> {{ copyCount === 1 ? 'time' : 'times' }} ·
          </template>
          <RouterLink :to="`/library/${deckId}`" class="fa-link">View the public page</RouterLink>
        </p>
      </template>

      <p v-if="!canPublish" class="share-note share-note-menu">
        Publishing to the library is disabled for your account. You can still share an unlisted link.
      </p>

      <div v-if="error" class="share-error share-note-menu">{{ error }}</div>
    </DropdownMenuContent>
  </DropdownMenu>

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
</template>

<style scoped>
.share-trigger { gap: 7px; }
.share-trigger-chevron { color: var(--ink-2); }

.share-option {
  align-items: flex-start !important;
  gap: 10px;
  padding-top: 8px !important;
  padding-bottom: 8px !important;
}
.share-option-icon { flex: none; margin-top: 2px; color: var(--ink-2); }
.share-option[aria-checked="true"] .share-option-icon { color: var(--accent-text); }
.share-option-text { display: flex; flex-direction: column; min-width: 0; flex: 1; gap: 1px; }
.share-option-label { font-size: 13px; font-weight: 500; }
.share-option-hint { font-size: 11.5px; color: var(--ink-2); white-space: normal; }
.share-option-check { flex: none; margin-top: 2px; color: var(--accent-text); }

.share-url-item {
  gap: 8px !important;
  align-items: center !important;
}
.share-url-text {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 12px;
  color: var(--ink-1);
}
.share-copy-action {
  flex: none;
  display: inline-flex;
  align-items: center;
  gap: 5px;
  font-size: 12px;
  font-weight: 500;
  color: var(--accent-text);
  white-space: nowrap;
}

.share-note { margin: 0; font-size: 12.5px; color: var(--ink-2); }
.share-note-menu { padding: 2px 8px 6px; }
.share-error { font-size: 13px; color: var(--c-again); }

.handle-field {
  display: flex;
  align-items: center;
  gap: 8px;
}
.handle-at { font-size: 15px; color: var(--ink-2); }
</style>
