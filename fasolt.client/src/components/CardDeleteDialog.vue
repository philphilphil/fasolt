<script setup lang="ts">
import { ref } from 'vue'
import { useCardsStore } from '@/stores/cards'
import { Button } from '@/components/ui/button'
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'

const props = defineProps<{
  open: boolean
  card: { id: string; decks?: { id: string; name: string }[] } | null
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  deleted: []
}>()

const cards = useCardsStore()
const error = ref('')

async function confirmDelete() {
  if (!props.card) return
  error.value = ''
  try {
    await cards.deleteCard(props.card.id)
    emit('update:open', false)
    emit('deleted')
  } catch {
    error.value = 'Failed to delete card.'
  }
}
</script>

<template>
  <Dialog :open="open" @update:open="emit('update:open', $event)">
    <DialogContent>
      <DialogHeader>
        <DialogTitle>Delete card</DialogTitle>
        <DialogDescription>
          <template v-if="card?.decks?.length">
            This card will be permanently deleted and removed from: <strong>{{ card.decks.map(d => d.name).join(', ') }}</strong>.
          </template>
          <template v-else>
            This card will be permanently deleted.
          </template>
        </DialogDescription>
      </DialogHeader>
      <div v-if="error" class="text-xs text-destructive">{{ error }}</div>
      <DialogFooter>
        <Button variant="outline" @click="emit('update:open', false)">Cancel</Button>
        <Button variant="destructive" @click="confirmDelete">Delete</Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
