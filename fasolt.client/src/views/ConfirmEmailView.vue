<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { apiFetch, isApiError } from '@/api/client'
import { useAuthStore } from '@/stores/auth'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const status = ref<'loading' | 'success' | 'error'>('loading')
const error = ref('')

onMounted(async () => {
  const userId = route.query.userId as string
  const token = route.query.token as string

  if (!userId || !token) {
    status.value = 'error'
    error.value = 'Invalid confirmation link.'
    return
  }

  try {
    await apiFetch('/account/confirm-email', {
      method: 'POST',
      body: JSON.stringify({ userId, token }),
    })
    // Refresh the local user object so the UI reflects the new
    // emailConfirmed state if the user happened to be signed in already.
    // Failure here is fine — confirmation succeeded server-side.
    try { await auth.fetchUser() } catch {}
    status.value = 'success'
  } catch (e) {
    status.value = 'error'
    if (isApiError(e) && e.errors) {
      error.value = Object.values(e.errors).flat().join(' ')
    } else {
      error.value = 'Something went wrong. Please try again.'
    }
  }
})

function goNext() {
  if (auth.isAuthenticated) {
    router.push('/study')
  } else {
    router.push('/login')
  }
}
</script>

<template>
  <Card class="border-border/60">
    <CardHeader>
      <CardTitle class="text-center text-base">Email confirmation</CardTitle>
    </CardHeader>
    <CardContent>
      <div v-if="status === 'loading'" class="text-center text-xs text-muted-foreground">
        Confirming your email&hellip;
      </div>
      <div v-else-if="status === 'success'" class="flex flex-col items-center gap-4">
        <p class="text-center text-sm">
          <span class="text-success">&#x2713;</span> Your email has been confirmed.
        </p>
        <Button class="w-full" @click="goNext">
          {{ auth.isAuthenticated ? 'Continue' : 'Sign in' }}
        </Button>
        <p v-if="!auth.isAuthenticated" class="text-center text-xs text-muted-foreground">
          Registered on the iOS app? Open Fasolt on your device and sign in there.
        </p>
      </div>
      <div v-else class="flex flex-col items-center gap-4">
        <p class="text-center text-xs text-destructive">{{ error }}</p>
        <Button variant="outline" class="w-full" @click="router.push('/login')">
          Log in to request a new link
        </Button>
      </div>
    </CardContent>
  </Card>
</template>
