<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/auth'
import { isApiError, createApiToken, listApiTokens, revokeApiToken } from '@/api/client'
import type { TokenListItem } from '@/api/client'

const auth = useAuthStore()

// Display name
const displayName = ref('')
const displayNameSuccess = ref(false)
const displayNameError = ref('')

// Email
const newEmail = ref('')
const emailCurrentPassword = ref('')
const emailSuccess = ref(false)
const emailError = ref('')

// API Tokens
const tokens = ref<TokenListItem[]>([])
const newTokenName = ref('')
const createdToken = ref<string | null>(null)
const tokenError = ref('')
const tokenLoading = ref(false)

// Password
const currentPassword = ref('')
const newPassword = ref('')
const confirmNewPassword = ref('')
const passwordSuccess = ref(false)
const passwordError = ref('')

onMounted(() => {
  displayName.value = auth.user?.displayName || ''
  newEmail.value = auth.user?.email || ''
  loadTokens()
})

async function loadTokens() {
  try {
    tokens.value = await listApiTokens()
  } catch {
    // ignore
  }
}

async function handleCreateToken() {
  tokenError.value = ''
  createdToken.value = null
  if (!newTokenName.value.trim()) {
    tokenError.value = 'Token name is required.'
    return
  }
  tokenLoading.value = true
  try {
    const result = await createApiToken({ name: newTokenName.value.trim() })
    createdToken.value = result.token
    newTokenName.value = ''
    await loadTokens()
  } catch {
    tokenError.value = 'Failed to create token.'
  } finally {
    tokenLoading.value = false
  }
}

async function handleRevokeToken(id: string) {
  try {
    await revokeApiToken(id)
    await loadTokens()
  } catch {
    tokenError.value = 'Failed to revoke token.'
  }
}

function formatDate(iso: string | null) {
  if (!iso) return '—'
  return new Date(iso).toLocaleDateString()
}

async function saveDisplayName() {
  displayNameSuccess.value = false
  displayNameError.value = ''
  try {
    await auth.updateProfile(displayName.value || null)
    displayNameSuccess.value = true
  } catch (e) {
    displayNameError.value = 'Failed to update display name.'
  }
}

async function saveEmail() {
  emailSuccess.value = false
  emailError.value = ''
  try {
    await auth.changeEmail(newEmail.value, emailCurrentPassword.value)
    emailCurrentPassword.value = ''
    emailSuccess.value = true
  } catch (e) {
    if (isApiError(e) && e.errors) {
      emailError.value = Object.values(e.errors).flat().join(' ')
    } else {
      emailError.value = 'Failed to update email.'
    }
  }
}

async function savePassword() {
  passwordSuccess.value = false
  passwordError.value = ''
  if (newPassword.value !== confirmNewPassword.value) {
    passwordError.value = 'Passwords do not match.'
    return
  }
  try {
    await auth.changePassword(currentPassword.value, newPassword.value)
    currentPassword.value = ''
    newPassword.value = ''
    confirmNewPassword.value = ''
    passwordSuccess.value = true
  } catch (e) {
    if (isApiError(e) && e.errors) {
      passwordError.value = Object.values(e.errors).flat().join(' ')
    } else {
      passwordError.value = 'Failed to update password.'
    }
  }
}
</script>

<template>
  <div class="flex flex-col gap-6">
    <h1 class="text-lg font-semibold tracking-tight">Settings</h1>

    <!-- Display Name -->
    <Card>
      <CardHeader>
        <CardTitle class="text-base">Display name</CardTitle>
      </CardHeader>
      <CardContent>
        <form class="flex flex-col gap-3" @submit.prevent="saveDisplayName">
          <div v-if="displayNameSuccess" class="rounded-md bg-green-500/10 px-3 py-2 text-sm text-green-600">Saved.</div>
          <div v-if="displayNameError" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ displayNameError }}</div>
          <Input v-model="displayName" placeholder="Your display name" />
          <Button type="submit" size="sm" class="self-start">Save</Button>
        </form>
      </CardContent>
    </Card>

    <!-- Email -->
    <Card>
      <CardHeader>
        <CardTitle class="text-base">Email address</CardTitle>
      </CardHeader>
      <CardContent>
        <form class="flex flex-col gap-3" @submit.prevent="saveEmail">
          <div v-if="emailSuccess" class="rounded-md bg-green-500/10 px-3 py-2 text-sm text-green-600">Email updated.</div>
          <div v-if="emailError" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ emailError }}</div>
          <div class="flex flex-col gap-1.5">
            <label for="new-email" class="text-sm font-medium">New email</label>
            <Input id="new-email" v-model="newEmail" type="email" required />
          </div>
          <div class="flex flex-col gap-1.5">
            <label for="email-password" class="text-sm font-medium">Current password</label>
            <Input id="email-password" v-model="emailCurrentPassword" type="password" required autocomplete="off" />
          </div>
          <Button type="submit" size="sm" class="self-start">Update email</Button>
        </form>
      </CardContent>
    </Card>

    <!-- Password -->
    <Card>
      <CardHeader>
        <CardTitle class="text-base">Change password</CardTitle>
      </CardHeader>
      <CardContent>
        <form class="flex flex-col gap-3" @submit.prevent="savePassword">
          <div v-if="passwordSuccess" class="rounded-md bg-green-500/10 px-3 py-2 text-sm text-green-600">Password changed.</div>
          <div v-if="passwordError" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ passwordError }}</div>
          <div class="flex flex-col gap-1.5">
            <label for="current-password" class="text-sm font-medium">Current password</label>
            <Input id="current-password" v-model="currentPassword" type="password" required autocomplete="current-password" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label for="new-password" class="text-sm font-medium">New password</label>
            <Input id="new-password" v-model="newPassword" type="password" required autocomplete="new-password" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label for="confirm-new-password" class="text-sm font-medium">Confirm new password</label>
            <Input id="confirm-new-password" v-model="confirmNewPassword" type="password" required autocomplete="new-password" />
          </div>
          <Button type="submit" size="sm" class="self-start">Change password</Button>
        </form>
      </CardContent>
    </Card>

    <!-- API Tokens -->
    <Card>
      <CardHeader>
        <CardTitle class="text-base">API Tokens</CardTitle>
      </CardHeader>
      <CardContent>
        <div class="flex flex-col gap-4">
          <p class="text-sm text-muted-foreground">Create tokens to authenticate API requests from AI agents and tools.</p>

          <div v-if="tokenError" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ tokenError }}</div>

          <!-- Token creation -->
          <form class="flex gap-2" @submit.prevent="handleCreateToken">
            <Input v-model="newTokenName" placeholder="Token name (e.g., Obsidian Agent)" class="flex-1" />
            <Button type="submit" size="sm" :disabled="tokenLoading">Create token</Button>
          </form>

          <!-- Show newly created token -->
          <div v-if="createdToken" class="rounded-md border border-green-500/30 bg-green-500/10 p-3">
            <p class="text-sm font-medium text-green-600 mb-1">Token created — copy it now, it won't be shown again:</p>
            <code class="block rounded bg-muted px-2 py-1 text-sm font-mono break-all select-all">{{ createdToken }}</code>
          </div>

          <!-- Token list -->
          <div v-if="tokens.length > 0" class="flex flex-col gap-2">
            <div
              v-for="token in tokens"
              :key="token.id"
              class="flex items-center justify-between rounded-md border px-3 py-2"
            >
              <div class="flex flex-col gap-0.5">
                <div class="flex items-center gap-2">
                  <span class="text-sm font-medium">{{ token.name }}</span>
                  <code class="text-xs text-muted-foreground">{{ token.tokenPrefix }}...</code>
                  <span v-if="token.isRevoked" class="text-xs text-destructive">Revoked</span>
                  <span v-else-if="token.isExpired" class="text-xs text-yellow-600">Expired</span>
                </div>
                <div class="text-xs text-muted-foreground">
                  Created {{ formatDate(token.createdAt) }}
                  <span v-if="token.lastUsedAt"> · Last used {{ formatDate(token.lastUsedAt) }}</span>
                  <span v-if="token.expiresAt"> · Expires {{ formatDate(token.expiresAt) }}</span>
                </div>
              </div>
              <Button
                v-if="!token.isRevoked"
                variant="ghost"
                size="sm"
                class="text-destructive hover:text-destructive"
                @click="handleRevokeToken(token.id)"
              >
                Revoke
              </Button>
            </div>
          </div>

          <p v-else class="text-sm text-muted-foreground">No tokens yet.</p>
        </div>
      </CardContent>
    </Card>
  </div>
</template>
