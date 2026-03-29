<script setup lang="ts">
import { ref } from 'vue'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/auth'
import { useRouter } from 'vue-router'

const auth = useAuthStore()
const router = useRouter()

const resending = ref(false)
const resent = ref(false)
const cooldown = ref(0)
let timer: ReturnType<typeof setInterval> | null = null

async function handleResend() {
  resending.value = true
  try {
    await auth.resendVerification()
    resent.value = true
    cooldown.value = 60
    timer = setInterval(() => {
      cooldown.value--
      if (cooldown.value <= 0 && timer) {
        clearInterval(timer)
        timer = null
        resent.value = false
      }
    }, 1000)
  } finally {
    resending.value = false
  }
}

async function handleLogout() {
  await auth.logout()
  router.push('/login')
}
</script>

<template>
  <Card class="border-border/60">
    <CardHeader>
      <CardTitle class="text-center text-base">Verify your email</CardTitle>
    </CardHeader>
    <CardContent class="flex flex-col items-center gap-4">
      <p class="text-center text-xs text-muted-foreground">
        We sent a verification link to <strong class="text-foreground">{{ auth.user?.email }}</strong>.
        Check your inbox and click the link to activate your account.
      </p>
      <Button
        variant="outline"
        class="w-full"
        :disabled="resending || cooldown > 0"
        @click="handleResend"
      >
        <template v-if="cooldown > 0">Resend in {{ cooldown }}s</template>
        <template v-else-if="resending">Sending...</template>
        <template v-else>Resend verification email</template>
      </Button>
      <button class="text-xs text-muted-foreground hover:underline" @click="handleLogout">
        Log out
      </button>
    </CardContent>
  </Card>
</template>
