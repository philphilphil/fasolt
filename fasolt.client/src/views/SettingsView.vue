<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/auth'
import { useSnapshotsStore } from '@/stores/snapshots'
import { apiFetch, isApiError } from '@/api/client'

const auth = useAuthStore()
const snapshotsStore = useSnapshotsStore()
const snapshotting = ref(false)
const snapshotSuccess = ref(false)
const snapshotCount = ref(0)

async function createSnapshot() {
  snapshotting.value = true
  snapshotSuccess.value = false
  try {
    const result = await snapshotsStore.createAll()
    snapshotCount.value = result.count
    snapshotSuccess.value = true
  } catch {
    // silent
  } finally {
    snapshotting.value = false
  }
}

const desiredRetention = ref(0.9)
const maximumInterval = ref(36500)
const schedulingSuccess = ref(false)
const schedulingError = ref('')
const schedulingLoading = ref(true)

async function loadSchedulingSettings() {
  schedulingLoading.value = true
  try {
    const settings = await apiFetch<{ desiredRetention: number; maximumInterval: number }>('/settings/scheduling')
    desiredRetention.value = settings.desiredRetention
    maximumInterval.value = settings.maximumInterval
  } catch {
    schedulingError.value = 'Failed to load scheduling settings.'
  } finally {
    schedulingLoading.value = false
  }
}

async function saveSchedulingSettings() {
  schedulingSuccess.value = false
  schedulingError.value = ''
  try {
    const settings = await apiFetch<{ desiredRetention: number; maximumInterval: number }>('/settings/scheduling', {
      method: 'PUT',
      body: JSON.stringify({
        desiredRetention: desiredRetention.value,
        maximumInterval: maximumInterval.value,
      }),
    })
    desiredRetention.value = settings.desiredRetention
    maximumInterval.value = settings.maximumInterval
    schedulingSuccess.value = true
  } catch (e) {
    if (isApiError(e) && e.errors) {
      schedulingError.value = Object.values(e.errors).flat().join(' ')
    } else {
      schedulingError.value = 'Failed to save scheduling settings.'
    }
  }
}

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
  newEmail.value = auth.user?.email || ''
  loadSchedulingSettings()
})

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
        <CardTitle class="text-sm">Snapshots</CardTitle>
      </CardHeader>
      <CardContent class="flex flex-col gap-3">
        <p class="text-xs text-muted-foreground">
          Create a backup of all your decks. Snapshots capture every card's content and study progress so you can restore them later if something goes wrong. The last 10 snapshots per deck are kept automatically.
        </p>
        <div v-if="snapshotSuccess" class="rounded border border-success/20 bg-success/10 px-3 py-2 text-xs text-success">
          Snapshot created for {{ snapshotCount }} deck{{ snapshotCount !== 1 ? 's' : '' }}.
        </div>
        <Button size="sm" class="self-start text-xs" :disabled="snapshotting" @click="createSnapshot">
          {{ snapshotting ? 'Creating...' : 'Create snapshot' }}
        </Button>
      </CardContent>
    </Card>

    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">Scheduling</CardTitle>
      </CardHeader>
      <CardContent>
        <form class="flex flex-col gap-4" @submit.prevent="saveSchedulingSettings">
          <div v-if="schedulingLoading" class="text-xs text-muted-foreground">Loading...</div>
          <template v-else>
            <div v-if="schedulingSuccess" class="rounded border border-success/20 bg-success/10 px-3 py-2 text-xs text-success">Settings saved.</div>
            <div v-if="schedulingError" class="rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive">{{ schedulingError }}</div>

            <div class="flex flex-col gap-1.5">
              <label for="desired-retention" class="text-xs font-medium">Desired retention</label>
              <Input id="desired-retention" v-model.number="desiredRetention" type="number" min="0.70" max="0.97" step="0.01" required />
              <p class="text-xs text-muted-foreground">
                How likely you want to remember a card when it comes up for review. Higher values (e.g. 0.95) mean more frequent reviews but stronger recall. Lower values (e.g. 0.85) mean fewer reviews but more forgetting. Changes apply to future reviews only — cards already scheduled keep their current due dates.
              </p>
            </div>

            <div class="flex flex-col gap-1.5">
              <label for="maximum-interval" class="text-xs font-medium">Maximum interval (days)</label>
              <Input id="maximum-interval" v-model.number="maximumInterval" type="number" min="1" max="36500" step="1" required />
              <p class="text-xs text-muted-foreground">
                The longest gap allowed between reviews, in days. For example, 365 means you'll see every card at least once a year. The default (36500 days ≈ 100 years) means there's effectively no cap.
              </p>
            </div>

            <Button type="submit" size="sm" class="self-start text-xs">Save scheduling settings</Button>
          </template>
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

  </div>
</template>
