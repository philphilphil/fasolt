<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { apiFetch, isApiError } from '@/api/client'
import { useAuthStore } from '@/stores/auth'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const loading = ref(true)
const error = ref('')

onMounted(async () => {
  const token = route.query.token as string
  const email = route.query.email as string

  if (!token || !email) {
    error.value = 'Invalid confirmation link.'
    loading.value = false
    return
  }

  try {
    await apiFetch('/account/confirm-email-change', {
      method: 'POST',
      body: JSON.stringify({ newEmail: email, token }),
    })
    await auth.fetchUser()
    setTimeout(() => router.push('/settings'), 1500)
  } catch (e) {
    if (isApiError(e) && e.errors) {
      error.value = Object.values(e.errors).flat().join(' ')
    } else {
      error.value = 'Invalid or expired confirmation link.'
    }
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <Card class="border-border/60">
    <CardHeader>
      <CardTitle class="text-center text-base">Email change</CardTitle>
    </CardHeader>
    <CardContent>
      <div v-if="loading" class="text-center text-xs text-muted-foreground">
        Confirming your new email...
      </div>
      <div v-else-if="error" class="flex flex-col items-center gap-4">
        <p class="text-center text-xs text-destructive">{{ error }}</p>
      </div>
      <div v-else class="flex flex-col items-center gap-2">
        <p class="text-center text-xs text-muted-foreground">
          Your email has been updated. Redirecting to settings...
        </p>
      </div>
    </CardContent>
  </Card>
</template>
