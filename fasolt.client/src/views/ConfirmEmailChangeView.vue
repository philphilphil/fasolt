<script setup lang="ts">
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { apiFetch, isApiError } from '@/api/client'
import { useAuthStore } from '@/stores/auth'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const newEmail = route.query.email as string
const token = route.query.token as string
const valid = Boolean(newEmail && token)

const confirming = ref(false)
const confirmed = ref(false)
const error = ref('')

async function handleConfirm() {
  confirming.value = true
  error.value = ''
  try {
    await apiFetch('/account/confirm-email-change', {
      method: 'POST',
      body: JSON.stringify({ newEmail, token }),
    })
    await auth.fetchUser()
    confirmed.value = true
    setTimeout(() => router.push('/settings'), 1500)
  } catch (e) {
    if (isApiError(e) && e.errors) {
      error.value = Object.values(e.errors).flat().join(' ')
    } else {
      error.value = 'Invalid or expired confirmation link.'
    }
  } finally {
    confirming.value = false
  }
}
</script>

<template>
  <Card class="border-border/60">
    <CardHeader>
      <CardTitle class="text-center text-base">Confirm email change</CardTitle>
    </CardHeader>
    <CardContent>
      <div v-if="!valid" class="flex flex-col items-center gap-4">
        <p class="text-center text-xs text-destructive">Invalid confirmation link.</p>
      </div>
      <div v-else-if="confirmed" class="flex flex-col items-center gap-2">
        <p class="text-center text-xs text-muted-foreground">
          Your email has been updated. Redirecting to settings...
        </p>
      </div>
      <div v-else class="flex flex-col items-center gap-4">
        <p class="text-center text-xs text-muted-foreground">
          Change your account email to <strong class="text-foreground">{{ newEmail }}</strong>?
        </p>
        <div v-if="error" class="w-full rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive">{{ error }}</div>
        <Button class="w-full" :disabled="confirming" @click="handleConfirm">
          {{ confirming ? 'Confirming...' : 'Confirm email change' }}
        </Button>
      </div>
    </CardContent>
  </Card>
</template>
