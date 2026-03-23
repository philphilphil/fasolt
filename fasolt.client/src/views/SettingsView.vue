<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { Copy, Check } from 'lucide-vue-next'
import { useAuthStore } from '@/stores/auth'
import { isApiError } from '@/api/client'
import { useDarkMode } from '@/composables/useDarkMode'

const auth = useAuthStore()
const { theme, setTheme } = useDarkMode()

const copiedStates = ref<Record<string, boolean>>({})
const origin = computed(() => window.location.origin)
const remoteClaudeCommand = computed(() =>
  `claude mcp add fasolt --transport http ${origin.value}/mcp`
)
const remoteCopilotConfig = computed(() =>
  JSON.stringify({
    mcpServers: {
      fasolt: {
        type: 'http',
        url: `${origin.value}/mcp`,
      },
    },
  }, null, 2)
)
function copyToClipboard(text: string, key: string) {
  navigator.clipboard.writeText(text)
  copiedStates.value[key] = true
  setTimeout(() => { copiedStates.value[key] = false }, 2000)
}

const displayName = ref('')
const displayNameSuccess = ref(false)
const displayNameError = ref('')

const newEmail = ref('')
const emailCurrentPassword = ref('')
const emailSuccess = ref(false)
const emailError = ref('')

const currentPassword = ref('')
const newPassword = ref('')
const confirmNewPassword = ref('')
const passwordSuccess = ref(false)
const passwordError = ref('')

onMounted(() => {
  displayName.value = auth.user?.displayName || ''
  newEmail.value = auth.user?.email || ''
})

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
    <h1 class="text-lg font-bold tracking-tight">Settings</h1>

    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">Display name</CardTitle>
      </CardHeader>
      <CardContent>
        <form class="flex flex-col gap-3" @submit.prevent="saveDisplayName">
          <div v-if="displayNameSuccess" class="rounded border border-success/20 bg-success/10 px-3 py-2 text-xs text-success">Saved.</div>
          <div v-if="displayNameError" class="rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive">{{ displayNameError }}</div>
          <Input v-model="displayName" placeholder="Your display name" />
          <Button type="submit" size="sm" class="self-start text-xs">Save</Button>
        </form>
      </CardContent>
    </Card>

    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">Email address</CardTitle>
      </CardHeader>
      <CardContent>
        <form class="flex flex-col gap-3" @submit.prevent="saveEmail">
          <div v-if="emailSuccess" class="rounded border border-success/20 bg-success/10 px-3 py-2 text-xs text-success">Email updated.</div>
          <div v-if="emailError" class="rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive">{{ emailError }}</div>
          <div class="flex flex-col gap-1.5">
            <label for="new-email" class="text-xs font-medium">New email</label>
            <Input id="new-email" v-model="newEmail" type="email" required />
          </div>
          <div class="flex flex-col gap-1.5">
            <label for="email-password" class="text-xs font-medium">Current password</label>
            <Input id="email-password" v-model="emailCurrentPassword" type="password" required autocomplete="off" />
          </div>
          <Button type="submit" size="sm" class="self-start text-xs">Update email</Button>
        </form>
      </CardContent>
    </Card>

    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">Change password</CardTitle>
      </CardHeader>
      <CardContent>
        <form class="flex flex-col gap-3" @submit.prevent="savePassword">
          <div v-if="passwordSuccess" class="rounded border border-success/20 bg-success/10 px-3 py-2 text-xs text-success">Password changed.</div>
          <div v-if="passwordError" class="rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive">{{ passwordError }}</div>
          <div class="flex flex-col gap-1.5">
            <label for="current-password" class="text-xs font-medium">Current password</label>
            <Input id="current-password" v-model="currentPassword" type="password" required autocomplete="current-password" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label for="new-password" class="text-xs font-medium">New password</label>
            <Input id="new-password" v-model="newPassword" type="password" required autocomplete="new-password" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label for="confirm-new-password" class="text-xs font-medium">Confirm new password</label>
            <Input id="confirm-new-password" v-model="confirmNewPassword" type="password" required autocomplete="new-password" />
          </div>
          <Button type="submit" size="sm" class="self-start text-xs">Change password</Button>
        </form>
      </CardContent>
    </Card>

    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">Appearance</CardTitle>
      </CardHeader>
      <CardContent>
        <div class="flex gap-1 rounded-lg border border-border p-1">
          <button
            v-for="opt in (['light', 'system', 'dark'] as const)"
            :key="opt"
            class="rounded-md px-4 py-1.5 text-xs capitalize transition-colors"
            :class="theme === opt ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:text-foreground'"
            @click="setTheme(opt)"
          >
            {{ opt }}
          </button>
        </div>
      </CardContent>
    </Card>

    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">MCP Setup</CardTitle>
      </CardHeader>
      <CardContent>
        <p class="text-xs text-muted-foreground mb-4 leading-relaxed">
          Ask your AI agent to read your local markdown notes and create flashcards.
          The agent uses the fasolt MCP server to push cards to your account.
        </p>

        <div class="flex flex-col gap-4">
          <div>
            <h3 class="text-xs font-medium mb-2">Claude Code</h3>
            <div class="relative">
              <pre class="rounded border border-border bg-secondary px-3 py-2.5 pr-10 text-xs overflow-x-auto whitespace-pre-wrap break-all">{{ remoteClaudeCommand }}</pre>
              <button
                class="absolute right-2 top-2 rounded p-1 text-muted-foreground hover:text-foreground transition-colors"
                @click="copyToClipboard(remoteClaudeCommand, 'claude')"
              >
                <Check v-if="copiedStates['claude']" class="h-3.5 w-3.5 text-success" />
                <Copy v-else class="h-3.5 w-3.5" />
              </button>
            </div>
          </div>

          <div>
            <h3 class="text-xs font-medium mb-2">GitHub Copilot CLI</h3>
            <p class="text-xs text-muted-foreground mb-2">
              Add to <code class="rounded border border-border bg-secondary px-1 py-0.5 text-[10px]">~/.copilot/mcp-config.json</code>:
            </p>
            <div class="relative">
              <pre class="rounded border border-border bg-secondary px-3 py-2.5 pr-10 text-xs overflow-x-auto">{{ remoteCopilotConfig }}</pre>
              <button
                class="absolute right-2 top-2 rounded p-1 text-muted-foreground hover:text-foreground transition-colors"
                @click="copyToClipboard(remoteCopilotConfig, 'copilot')"
              >
                <Check v-if="copiedStates['copilot']" class="h-3.5 w-3.5 text-success" />
                <Copy v-else class="h-3.5 w-3.5" />
              </button>
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  </div>
</template>
