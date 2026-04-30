<script setup lang="ts">
import { ref, watch } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useRouter } from 'vue-router'
import { isApiError } from '@/api/client'
import { Button } from '@/components/ui/button'
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'

const props = defineProps<{ open: boolean }>()
const emit = defineEmits<{ 'update:open': [value: boolean] }>()

const auth = useAuthStore()
const router = useRouter()
const error = ref('')
const deleting = ref(false)

watch(() => props.open, (val) => {
  if (val) {
    error.value = ''
  }
})

async function confirmDelete() {
  error.value = ''
  deleting.value = true
  try {
    await auth.deleteAccount()
    emit('update:open', false)
    router.push('/')
  } catch (e) {
    if (isApiError(e) && e.errors) {
      error.value = Object.values(e.errors).flat().join(' ')
    } else {
      error.value = 'Failed to delete account.'
    }
  } finally {
    deleting.value = false
  }
}
</script>

<template>
  <Dialog :open="open" @update:open="emit('update:open', $event)">
    <DialogContent>
      <DialogHeader>
        <DialogTitle>Delete account</DialogTitle>
        <DialogDescription>
          This action is permanent and cannot be undone. All your cards, decks, and study progress will be deleted.
        </DialogDescription>
      </DialogHeader>
      <div v-if="error" class="rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive">{{ error }}</div>
      <DialogFooter>
        <Button variant="outline" @click="emit('update:open', false)">Cancel</Button>
        <Button variant="destructive" :disabled="deleting" @click="confirmDelete">
          {{ deleting ? 'Deleting...' : 'Delete my account' }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
