<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/auth'
import { useSnapshotsStore } from '@/stores/snapshots'
import { apiFetch, isApiError } from '@/api/client'
import DeleteAccountDialog from '@/components/DeleteAccountDialog.vue'

const auth = useAuthStore()
const snapshotsStore = useSnapshotsStore()
const snapshotting = ref(false)
const snapshotSuccess = ref(false)
const snapshotError = ref('')
const snapshotCreated = ref(0)
const snapshotSkipped = ref(0)

async function createSnapshot() {
  snapshotting.value = true
  snapshotSuccess.value = false
  snapshotError.value = ''
  try {
    const result = await snapshotsStore.createAll()
    snapshotCreated.value = result.created
    snapshotSkipped.value = result.skipped
    snapshotSuccess.value = true
    snapshotsStore.fetchRecent()
  } catch {
    snapshotError.value = 'Failed to create snapshot. Please try again.'
  } finally {
    snapshotting.value = false
  }
}

type SchedulingSettings = {
  desiredRetention: number
  maximumInterval: number
  dayStartHour: number
  timeZone: string | null
}

const desiredRetention = ref(0.9)
const maximumInterval = ref(36500)
const dayStartHour = ref(4)
const schedulingSuccess = ref(false)
const schedulingError = ref('')
const schedulingLoading = ref(true)

const browserTimeZone = (() => {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC'
  } catch {
    return 'UTC'
  }
})()

const hourOptions = Array.from({ length: 24 }, (_, i) => i)

function formatHourLabel(h: number): string {
  const hh = h.toString().padStart(2, '0')
  return `${hh}:00`
}

async function pushSchedulingSettings() {
  const settings = await apiFetch<SchedulingSettings>('/settings/scheduling', {
    method: 'PUT',
    body: JSON.stringify({
      desiredRetention: desiredRetention.value,
      maximumInterval: maximumInterval.value,
      dayStartHour: dayStartHour.value,
      timeZone: browserTimeZone,
    }),
  })
  desiredRetention.value = settings.desiredRetention
  maximumInterval.value = settings.maximumInterval
  dayStartHour.value = settings.dayStartHour
}

async function loadSchedulingSettings() {
  schedulingLoading.value = true
  try {
    const settings = await apiFetch<SchedulingSettings>('/settings/scheduling')
    desiredRetention.value = settings.desiredRetention
    maximumInterval.value = settings.maximumInterval
    dayStartHour.value = settings.dayStartHour

    // First-time initialization: server has no time zone for this user
    // (e.g. external OAuth signup that bypassed the registration form).
    // Push the browser zone so daily rollover is correct from day one.
    if (!settings.timeZone) {
      try {
        await pushSchedulingSettings()
      } catch {
        // best-effort; user can still set it manually via Save
      }
    }
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
    await pushSchedulingSettings()
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

const deleteDialogOpen = ref(false)

const snapshotCount = computed(() => snapshotsStore.snapshots.length)
const lastSnapshot = computed(() => {
  if (snapshotsStore.snapshots.length === 0) return null
  const date = new Date(snapshotsStore.snapshots[0].createdAt)
  return date.toLocaleDateString(undefined, { dateStyle: 'medium' })
})
const totalCardsBacked = computed(() =>
  snapshotsStore.snapshots.reduce((sum, s) => sum + s.cardCount, 0)
)

onMounted(() => {
  newEmail.value = auth.user?.email || ''
  loadSchedulingSettings()
  snapshotsStore.fetchRecent()
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

    <div class="grid grid-cols-1 gap-6 md:grid-cols-2">
      <div class="flex flex-col gap-6">
        <Card class="border-border/60">
          <CardHeader>
            <CardTitle class="text-sm">Snapshots</CardTitle>
          </CardHeader>
          <CardContent class="flex flex-col gap-3">
            <p class="text-xs text-muted-foreground">
              Create a backup of all your decks. Snapshots capture every card's content so you can restore it later if something goes wrong. The last 10 snapshots per deck are kept automatically. Study progress is not affected by restoring — only card content (front, back, images, source) is reverted.
            </p>
            <div v-if="snapshotSuccess" class="rounded border border-success/20 bg-success/10 px-3 py-2 text-xs text-success">
              <template v-if="snapshotCreated > 0 && snapshotSkipped === 0">
                Snapshot created for {{ snapshotCreated }} deck{{ snapshotCreated !== 1 ? 's' : '' }}.
              </template>
              <template v-else-if="snapshotCreated > 0 && snapshotSkipped > 0">
                Snapshot created for {{ snapshotCreated }} deck{{ snapshotCreated !== 1 ? 's' : '' }}. {{ snapshotSkipped }} unchanged, skipped.
              </template>
              <template v-else>
                All decks unchanged — no snapshots needed.
              </template>
            </div>
            <div v-if="snapshotError" class="rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive">{{ snapshotError }}</div>
            <div v-if="snapshotCount > 0" class="flex gap-6 text-xs">
              <div class="flex flex-col">
                <span class="text-muted-foreground">Snapshots</span>
                <span class="font-medium">{{ snapshotCount }}</span>
              </div>
              <div class="flex flex-col">
                <span class="text-muted-foreground">Cards backed up</span>
                <span class="font-medium">{{ totalCardsBacked }}</span>
              </div>
              <div v-if="lastSnapshot" class="flex flex-col">
                <span class="text-muted-foreground">Last snapshot</span>
                <span class="font-medium">{{ lastSnapshot }}</span>
              </div>
            </div>
            <Button size="sm" class="self-start text-xs" :disabled="snapshotting" @click="createSnapshot">
              {{ snapshotting ? 'Creating...' : 'Create snapshot' }}
            </Button>
          </CardContent>
        </Card>

        <Card class="border-border/60">
          <CardHeader>
            <CardTitle class="text-sm">Account</CardTitle>
          </CardHeader>
          <CardContent class="flex flex-col gap-1">
            <div class="flex items-center gap-2 text-xs">
              <span class="text-muted-foreground">Signed in as</span>
              <span class="font-medium">{{ auth.user?.displayName || auth.user?.email }}</span>
            </div>
            <div class="flex items-center gap-2 text-xs">
              <span class="text-muted-foreground">Account type</span>
              <span v-if="auth.user?.externalProvider === 'GitHub'" class="inline-flex items-center gap-1 font-medium">
                <svg class="h-3.5 w-3.5" viewBox="0 0 16 16" fill="currentColor"><path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"/></svg>
                GitHub
              </span>
              <span v-else-if="auth.user?.externalProvider === 'Apple'" class="inline-flex items-center gap-1 font-medium">
                <svg class="h-3.5 w-3.5" viewBox="0 0 24 24" fill="currentColor"><path d="M17.05 20.28c-.98.95-2.05.88-3.08.4-1.09-.5-2.08-.52-3.23 0-1.44.64-2.2.52-3.06-.4C3.79 16.17 4.36 9.02 8.93 8.76c1.28.07 2.17.72 2.91.77.93-.19 1.82-.87 2.83-.79 1.19.1 2.08.6 2.67 1.5-2.44 1.47-1.86 4.7.37 5.6-.44 1.16-1.01 2.3-2.66 4.44zM12.03 8.7c-.15-2.34 1.8-4.3 3.97-4.5.29 2.56-2.34 4.48-3.97 4.5z"/></svg>
                Apple
              </span>
              <span v-else-if="auth.isExternalAccount" class="font-medium">{{ auth.user?.externalProvider }}</span>
              <span v-else class="font-medium">Email &amp; password</span>
            </div>
          </CardContent>
        </Card>
      </div>

      <Card class="border-border/60">
        <CardHeader>
          <CardTitle class="text-sm">Scheduling</CardTitle>
          <p class="text-xs text-muted-foreground">
            Adjust how the FSRS algorithm schedules your reviews.
            <RouterLink to="/algorithm" class="text-accent hover:underline">How the algorithm works</RouterLink>
          </p>
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

              <div class="flex flex-col gap-1.5">
                <label for="day-start-hour" class="text-xs font-medium">Day starts at</label>
                <select
                  id="day-start-hour"
                  v-model.number="dayStartHour"
                  class="flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-xs shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                >
                  <option v-for="h in hourOptions" :key="h" :value="h">{{ formatHourLabel(h) }}</option>
                </select>
                <p class="text-xs text-muted-foreground">
                  Hour at which a new study day begins, in your browser's time zone ({{ browserTimeZone }}). Cards scheduled a day or more in advance become due all at once at this time, instead of trickling in throughout the day. Sub-day learning steps still fire at their exact times.
                </p>
              </div>

              <Button type="submit" size="sm" class="self-start text-xs">Save scheduling settings</Button>
            </template>
          </form>
        </CardContent>
      </Card>
    </div>

    <div v-if="!auth.isExternalAccount" class="grid grid-cols-1 items-start gap-6 md:grid-cols-2">
      <Card class="border-border/60">
        <CardHeader>
          <CardTitle class="text-sm">Email address</CardTitle>
        </CardHeader>
        <CardContent>
          <form class="flex flex-col gap-3" @submit.prevent="saveEmail">
            <div v-if="emailSuccess" class="rounded border border-success/20 bg-success/10 px-3 py-2 text-xs text-success">Verification email sent. Check your inbox to confirm the change.</div>
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

    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">Your data</CardTitle>
      </CardHeader>
      <CardContent class="flex flex-col gap-3">
        <p class="text-xs text-muted-foreground">
          Download a copy of all your data (cards, decks, study progress, snapshots) as a JSON file, or permanently delete your account.
        </p>
        <div class="flex gap-2">
          <a href="/api/account/export" class="inline-flex">
            <Button size="sm" class="text-xs">Export data</Button>
          </a>
          <Button size="sm" variant="destructive" class="text-xs" @click="deleteDialogOpen = true">
            Delete account
          </Button>
        </div>
      </CardContent>
    </Card>

    <DeleteAccountDialog v-model:open="deleteDialogOpen" />

  </div>
</template>
