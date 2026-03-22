<script setup lang="ts">
import { ref } from 'vue'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()

const email = ref('')
const sent = ref(false)
const loading = ref(false)

async function handleSubmit() {
  loading.value = true
  try {
    await auth.forgotPassword(email.value)
  } finally {
    sent.value = true
    loading.value = false
  }
}
</script>

<template>
  <Card>
    <CardHeader>
      <CardTitle class="text-center text-lg">Reset password</CardTitle>
    </CardHeader>
    <CardContent>
      <template v-if="sent">
        <p class="text-center text-sm text-muted-foreground">
          If an account exists for <strong>{{ email }}</strong>, we sent a password reset link.
        </p>
        <div class="mt-4 text-center">
          <RouterLink to="/login" class="text-sm text-accent hover:underline">Back to login</RouterLink>
        </div>
      </template>
      <form v-else class="flex flex-col gap-4" @submit.prevent="handleSubmit">
        <p class="text-sm text-muted-foreground">Enter your email and we'll send you a reset link.</p>
        <div class="flex flex-col gap-1.5">
          <label for="email" class="text-sm font-medium">Email</label>
          <Input id="email" v-model="email" type="email" required autocomplete="email" />
        </div>
        <Button type="submit" class="w-full" :disabled="loading">
          {{ loading ? 'Sending\u2026' : 'Send reset link' }}
        </Button>
        <p class="text-center text-sm">
          <RouterLink to="/login" class="text-muted-foreground hover:underline">Back to login</RouterLink>
        </p>
      </form>
    </CardContent>
  </Card>
</template>
