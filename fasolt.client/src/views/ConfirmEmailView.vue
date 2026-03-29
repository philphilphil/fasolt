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

const error = ref('')

onMounted(async () => {
  const userId = route.query.userId as string
  const token = route.query.token as string

  if (!userId || !token) {
    error.value = 'Invalid confirmation link.'
    return
  }

  try {
    await apiFetch('/account/confirm-email', {
      method: 'POST',
      body: JSON.stringify({ userId, token }),
    })
    await auth.fetchUser()
    router.push('/study')
  } catch (e) {
    if (isApiError(e) && e.errors) {
      error.value = Object.values(e.errors).flat().join(' ')
    } else {
      error.value = 'Something went wrong. Please try again.'
    }
  }
})
</script>

<template>
  <Card class="border-border/60">
    <CardHeader>
      <CardTitle class="text-center text-base">Email confirmation</CardTitle>
    </CardHeader>
    <CardContent>
      <div v-if="error" class="flex flex-col items-center gap-4">
        <p class="text-center text-xs text-destructive">{{ error }}</p>
        <Button variant="outline" class="w-full" @click="router.push('/login')">
          Log in to request a new link
        </Button>
      </div>
      <div v-else class="text-center text-xs text-muted-foreground">
        Confirming your email...
      </div>
    </CardContent>
  </Card>
</template>
