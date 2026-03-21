<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useCardsStore } from '@/stores/cards'
import { useMarkdown } from '@/composables/useMarkdown'
import type { Card } from '@/types'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'

const route = useRoute()
const router = useRouter()
const cardsStore = useCardsStore()
const { render } = useMarkdown()

const card = ref<Card | null>(null)
const loading = ref(true)

const editing = ref(false)
const front = ref('')
const back = ref('')
const saving = ref(false)
const error = ref('')

onMounted(async () => {
  try {
    card.value = await cardsStore.getCard(route.params.id as string)
  } catch {
    router.replace('/cards')
  } finally {
    loading.value = false
  }
})

function startEdit() {
  if (!card.value) return
  front.value = card.value.front
  back.value = card.value.back
  error.value = ''
  editing.value = true
}

async function save() {
  if (!card.value || !front.value.trim() || !back.value.trim()) {
    error.value = 'Front and back are required.'
    return
  }
  saving.value = true
  error.value = ''
  try {
    card.value = await cardsStore.updateCard(card.value.id, {
      front: front.value,
      back: back.value,
    })
    editing.value = false
  } catch {
    error.value = 'Failed to update card.'
  } finally {
    saving.value = false
  }
}

async function handleDelete() {
  if (!card.value) return
  await cardsStore.deleteCard(card.value.id)
  router.replace('/cards')
}
</script>

<template>
  <div v-if="loading" class="py-12 text-center text-sm text-muted-foreground">Loading...</div>

  <div v-else-if="card" class="space-y-6">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-3">
        <Button variant="ghost" size="sm" class="h-7 text-xs" @click="router.push('/cards')">
          &larr; Cards
        </Button>
        <Badge variant="outline" class="text-[10px]">{{ card.cardType }}</Badge>
        <Badge variant="outline" class="text-[10px]">{{ card.state }}</Badge>
      </div>
      <div class="flex items-center gap-2">
        <Button v-if="!editing" variant="outline" size="sm" class="h-7 text-xs" @click="startEdit">Edit</Button>
        <Button
          variant="outline"
          size="sm"
          class="h-7 text-xs text-destructive hover:text-destructive"
          @click="handleDelete"
        >
          Delete
        </Button>
      </div>
    </div>

    <!-- Metadata -->
    <div class="flex flex-wrap gap-x-6 gap-y-1 text-xs text-muted-foreground">
      <span v-if="card.sourceHeading">Section: {{ card.sourceHeading }}</span>
      <span v-if="card.decks.length > 0">Decks: {{ card.decks.map(d => d.name).join(', ') }}</span>
      <span>Created: {{ new Date(card.createdAt).toLocaleDateString() }}</span>
      <span v-if="card.dueAt">Due: {{ new Date(card.dueAt).toLocaleDateString() }}</span>
    </div>

    <!-- Edit mode -->
    <div v-if="editing" class="space-y-4">
      <div class="space-y-1">
        <label class="text-xs font-medium text-muted-foreground">Front (question)</label>
        <textarea
          v-model="front"
          class="w-full rounded-md border border-border bg-transparent px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
          rows="3"
        />
      </div>
      <div class="space-y-1">
        <label class="text-xs font-medium text-muted-foreground">Back (answer)</label>
        <textarea
          v-model="back"
          class="w-full rounded-md border border-border bg-transparent px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
          rows="8"
        />
      </div>
      <div v-if="error" class="text-xs text-destructive">{{ error }}</div>
      <div class="flex gap-2">
        <Button size="sm" :disabled="saving" @click="save">
          {{ saving ? 'Saving...' : 'Save' }}
        </Button>
        <Button variant="outline" size="sm" @click="editing = false">Cancel</Button>
      </div>
    </div>

    <!-- View mode -->
    <div v-else class="space-y-4">
      <div class="space-y-1">
        <label class="text-xs font-medium text-muted-foreground">Front</label>
        <div class="prose prose-sm dark:prose-invert max-w-none rounded-md border border-border p-3" v-html="render(card.front)" />
      </div>
      <div class="space-y-1">
        <label class="text-xs font-medium text-muted-foreground">Back</label>
        <div class="prose prose-sm dark:prose-invert max-w-none rounded-md border border-border p-3" v-html="render(card.back)" />
      </div>
    </div>
  </div>
</template>
