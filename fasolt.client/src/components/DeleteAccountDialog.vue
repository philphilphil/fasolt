<script setup lang="ts">
import { ref, computed } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useRouter } from 'vue-router'
import { isApiError } from '@/api/client'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'

const props = defineProps<{ open: boolean }>()
const emit = defineEmits<{ 'update:open': [value: boolean] }>()

const auth = useAuthStore()
const router = useRouter()
const password = ref('')
const confirmIdentity = ref('')
const error = ref('')
const deleting = ref(false)

const isExternal = computed(() => auth.isExternalAccount)

async function confirmDelete() {
  error.value = ''
  deleting.value = true
  try {
    if (isExternal.value) {
      await auth.deleteAccount(undefined, confirmIdentity.value)
    } else {
      await auth.deleteAccount(password.value)
    }
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
      <div class="flex flex-col gap-3">
        <div v-if="error" class="rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive">{{ error }}</div>
        <div v-if="isExternal" class="flex flex-col gap-1.5">
          <label for="confirm-identity" class="text-xs font-medium">
            Type your username to confirm: <span class="font-mono select-all">{{ auth.user?.displayName }}</span>
          </label>
          <Input id="confirm-identity" v-model="confirmIdentity" placeholder="username" />
        </div>
        <div v-else class="flex flex-col gap-1.5">
          <label for="delete-password" class="text-xs font-medium">Enter your password to confirm</label>
          <Input id="delete-password" v-model="password" type="password" autocomplete="off" />
        </div>
      </div>
      <DialogFooter>
        <Button variant="outline" @click="emit('update:open', false)">Cancel</Button>
        <Button variant="destructive" :disabled="deleting" @click="confirmDelete">
          {{ deleting ? 'Deleting...' : 'Delete my account' }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
