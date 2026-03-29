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

const userId = route.query.userId as string
const token = route.query.token as string
const valid = Boolean(userId && token)

const confirming = ref(false)
const confirmed = ref(false)
const error = ref('')

async function handleConfirm() {
  confirming.value = true
  error.value = ''
  try {
    await apiFetch('/account/confirm-email', {
      method: 'POST',
      body: JSON.stringify({ userId, token }),
    })
    await auth.fetchUser()
    confirmed.value = true
    setTimeout(() => router.push('/study'), 1500)
  } catch (e) {
    if (isApiError(e) && e.errors) {
      error.value = Object.values(e.errors).flat().join(' ')
    } else {
      error.value = 'Something went wrong. Please try again.'
    }
  } finally {
    confirming.value = false
  }
}
</script>

<template>
  <Card class="border-border/60">
    <CardHeader>
      <CardTitle class="text-center text-base">Email confirmation</CardTitle>
    </CardHeader>
    <CardContent>
      <div v-if="!valid" class="flex flex-col items-center gap-4">
        <p class="text-center text-xs text-destructive">Invalid confirmation link.</p>
        <Button variant="outline" class="w-full" @click="router.push('/login')">
          Log in to request a new link
        </Button>
      </div>
      <div v-else-if="confirmed" class="flex flex-col items-center gap-2">
        <p class="text-center text-xs text-muted-foreground">
          Your email has been verified. Redirecting...
        </p>
      </div>
      <div v-else class="flex flex-col items-center gap-4">
        <p class="text-center text-xs text-muted-foreground">
          Click the button below to verify your email address.
        </p>
        <div v-if="error" class="w-full rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive">{{ error }}</div>
        <Button class="w-full" :disabled="confirming" @click="handleConfirm">
          {{ confirming ? 'Confirming...' : 'Verify my email' }}
        </Button>
      </div>
    </CardContent>
  </Card>
</template>
