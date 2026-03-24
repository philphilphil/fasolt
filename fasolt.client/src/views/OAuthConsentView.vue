<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'

const route = useRoute()

const clientName = ref('')
const permissions = ref<string[]>([])
const loading = ref(true)
const submitting = ref(false)
const error = ref('')

const clientId = route.query.client_id as string

onMounted(async () => {
  if (!clientId) {
    error.value = 'Missing client_id parameter.'
    loading.value = false
    return
  }

  try {
    const res = await fetch(`/api/oauth/consent-info?client_id=${encodeURIComponent(clientId)}`, {
      credentials: 'include',
    })
    if (!res.ok) {
      error.value = 'Unknown application.'
      loading.value = false
      return
    }
    const data = await res.json()
    clientName.value = data.clientName
    permissions.value = data.permissions ?? []
  } catch {
    error.value = 'Failed to load application info.'
  } finally {
    loading.value = false
  }
})

async function handleDecision(approved: boolean) {
  submitting.value = true
  error.value = ''
  try {
    const res = await fetch('/api/oauth/consent', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ clientId, approved }),
    })
    if (!res.ok) {
      error.value = 'Something went wrong. Please try again.'
      return
    }
    const data = await res.json()
    window.location.href = data.redirectUrl
  } catch {
    error.value = 'Something went wrong. Please try again.'
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <Card class="border-border/60">
    <CardHeader>
      <CardTitle class="text-center text-base">Authorize application</CardTitle>
    </CardHeader>
    <CardContent>
      <div v-if="loading" class="text-center text-sm text-muted-foreground py-4">
        Loading...
      </div>

      <div v-else-if="error" class="flex flex-col gap-4">
        <div class="rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive">
          {{ error }}
        </div>
      </div>

      <div v-else class="flex flex-col gap-5">
        <div class="text-center">
          <p class="text-sm">
            <span class="font-semibold">{{ clientName }}</span>
            wants to access your account.
          </p>
        </div>

        <div v-if="permissions.length" class="rounded border border-border/60 bg-muted/30 px-3 py-2.5">
          <p class="text-xs font-medium text-muted-foreground mb-1.5">This will allow the application to:</p>
          <ul class="text-xs space-y-1">
            <li v-for="permission in permissions" :key="permission" class="flex items-center gap-1.5">
              <span class="text-muted-foreground">&#8226;</span>
              <span>{{ permission }}</span>
            </li>
          </ul>
        </div>

        <div class="flex flex-col gap-2">
          <Button
            class="w-full"
            :disabled="submitting"
            @click="handleDecision(true)"
          >
            {{ submitting ? 'Authorizing\u2026' : 'Authorize' }}
          </Button>
          <Button
            variant="outline"
            class="w-full"
            :disabled="submitting"
            @click="handleDecision(false)"
          >
            Deny
          </Button>
        </div>
      </div>
    </CardContent>
  </Card>
</template>
