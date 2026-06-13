<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { apiFetch, isApiError } from '@/api/client'
import DeleteAccountDialog from '@/components/DeleteAccountDialog.vue'

const auth = useAuthStore()

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
      } catch (e) {
        console.warn('Failed to initialize time zone on first load', e)
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
  <div class="settings-page">
    <h1 class="page-title">Settings</h1>

    <div class="settings-grid">
      <div class="settings-col">
        <!-- Account -->
        <section class="fa-section">
          <h2 class="fa-section-head">Account</h2>
          <div class="meta-rows">
            <div class="meta-row">
              <span class="meta-label">Signed in as</span>
              <span class="meta-value">{{ auth.user?.displayName || auth.user?.email }}</span>
            </div>
            <div class="meta-row">
              <span class="meta-label">Account type</span>
              <span v-if="auth.user?.externalProvider === 'GitHub'" class="meta-value provider">
                <svg class="provider-icon" viewBox="0 0 16 16" fill="currentColor"><path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z"/></svg>
                GitHub
              </span>
              <span v-else-if="auth.user?.externalProvider === 'Apple'" class="meta-value provider">
                <svg class="provider-icon" viewBox="0 0 24 24" fill="currentColor"><path d="M17.05 20.28c-.98.95-2.05.88-3.08.4-1.09-.5-2.08-.52-3.23 0-1.44.64-2.2.52-3.06-.4C3.79 16.17 4.36 9.02 8.93 8.76c1.28.07 2.17.72 2.91.77.93-.19 1.82-.87 2.83-.79 1.19.1 2.08.6 2.67 1.5-2.44 1.47-1.86 4.7.37 5.6-.44 1.16-1.01 2.3-2.66 4.44zM12.03 8.7c-.15-2.34 1.8-4.3 3.97-4.5.29 2.56-2.34 4.48-3.97 4.5z"/></svg>
                Apple
              </span>
              <span v-else-if="auth.isExternalAccount" class="meta-value">{{ auth.user?.externalProvider }}</span>
              <span v-else class="meta-value">Email &amp; password</span>
            </div>
          </div>
        </section>

        <!-- Email address -->
        <section v-if="!auth.isExternalAccount" class="fa-section">
          <h2 class="fa-section-head">Email address</h2>
          <form class="fa-form" @submit.prevent="saveEmail">
            <p v-if="emailSuccess" class="msg msg-ok">Verification email sent. Check your inbox to confirm the change.</p>
            <p v-if="emailError" class="msg msg-err">{{ emailError }}</p>
            <div class="field">
              <label for="new-email" class="field-label">New email</label>
              <input id="new-email" v-model="newEmail" type="email" required class="fa-input" />
            </div>
            <div class="field">
              <label for="email-password" class="field-label">Current password</label>
              <input id="email-password" v-model="emailCurrentPassword" type="password" required autocomplete="off" class="fa-input" />
            </div>
            <button type="submit" class="fa-btn fa-btn-primary self-start">Update email</button>
          </form>
        </section>

        <!-- Change password -->
        <section v-if="!auth.isExternalAccount" class="fa-section">
          <h2 class="fa-section-head">Change password</h2>
          <form class="fa-form" @submit.prevent="savePassword">
            <p v-if="passwordSuccess" class="msg msg-ok">Password changed.</p>
            <p v-if="passwordError" class="msg msg-err">{{ passwordError }}</p>
            <div class="field">
              <label for="current-password" class="field-label">Current password</label>
              <input id="current-password" v-model="currentPassword" type="password" required autocomplete="current-password" class="fa-input" />
            </div>
            <div class="field">
              <label for="new-password" class="field-label">New password</label>
              <input id="new-password" v-model="newPassword" type="password" required autocomplete="new-password" class="fa-input" />
            </div>
            <div class="field">
              <label for="confirm-new-password" class="field-label">Confirm new password</label>
              <input id="confirm-new-password" v-model="confirmNewPassword" type="password" required autocomplete="new-password" class="fa-input" />
            </div>
            <button type="submit" class="fa-btn fa-btn-primary self-start">Change password</button>
          </form>
        </section>
      </div>

      <div class="settings-col">
        <!-- Scheduling -->
        <section class="fa-section">
          <h2 class="fa-section-head">Scheduling</h2>
          <p class="section-desc">
            Adjust how the FSRS algorithm schedules your reviews.
            <RouterLink to="/algorithm" class="fa-inline-link">How the algorithm works</RouterLink>
          </p>
          <form class="fa-form" @submit.prevent="saveSchedulingSettings">
            <div v-if="schedulingLoading" class="loading-line">Loading…</div>
            <template v-else>
              <p v-if="schedulingSuccess" class="msg msg-ok">Settings saved.</p>
              <p v-if="schedulingError" class="msg msg-err">{{ schedulingError }}</p>

              <div class="field">
                <label for="desired-retention" class="field-label">Desired retention</label>
                <input id="desired-retention" v-model.number="desiredRetention" type="number" min="0.70" max="0.97" step="0.01" required class="fa-input" />
                <p class="field-hint">
                  How likely you want to remember a card when it comes up for review. Higher values (e.g. 0.95) mean more frequent reviews but stronger recall. Lower values (e.g. 0.85) mean fewer reviews but more forgetting. Changes apply to future reviews only — cards already scheduled keep their current due dates.
                </p>
              </div>

              <div class="field">
                <label for="maximum-interval" class="field-label">Maximum interval (days)</label>
                <input id="maximum-interval" v-model.number="maximumInterval" type="number" min="1" max="36500" step="1" required class="fa-input" />
                <p class="field-hint">
                  The longest gap allowed between reviews, in days. For example, 365 means you'll see every card at least once a year. The default (36500 days ≈ 100 years) means there's effectively no cap.
                </p>
              </div>

              <div class="field">
                <label for="day-start-hour" class="field-label">Day starts at</label>
                <select id="day-start-hour" v-model.number="dayStartHour" class="fa-input fa-select">
                  <option v-for="h in hourOptions" :key="h" :value="h">{{ formatHourLabel(h) }}</option>
                </select>
                <p class="field-hint">
                  Hour at which a new study day begins, in your browser's time zone ({{ browserTimeZone }}). Cards scheduled a day or more in advance become due all at once at this time, instead of trickling in throughout the day. Sub-day learning steps still fire at their exact times.
                </p>
              </div>

              <button type="submit" class="fa-btn fa-btn-primary self-start">Save scheduling settings</button>
            </template>
          </form>
        </section>

        <!-- Your data -->
        <section class="fa-section">
          <h2 class="fa-section-head">Your data</h2>
          <p class="section-desc">
            Download a copy of all your data (cards, decks, study progress, snapshots) as a JSON file, or permanently delete your account.
          </p>
          <div class="data-actions">
            <a href="/api/account/export" class="fa-btn">Export data</a>
            <button type="button" class="fa-btn fa-btn-danger" @click="deleteDialogOpen = true">Delete account</button>
          </div>
        </section>
      </div>
    </div>

    <DeleteAccountDialog v-model:open="deleteDialogOpen" />
  </div>
</template>

<style scoped>
.settings-page {
  padding: 40px 8px 48px;
  display: flex;
  flex-direction: column;
  gap: 28px;
  max-width: 980px;
  margin: 0 auto;
  width: 100%;
}

.settings-grid {
  display: grid;
  grid-template-columns: 1fr;
  align-items: start;
  gap: 36px;
}
@media (min-width: 768px) {
  .settings-grid { grid-template-columns: 1fr 1fr; gap: 40px 48px; }
}
.settings-col { display: flex; flex-direction: column; gap: 36px; }

/* Section — whitespace + a quiet head, no boxed card */
.fa-section { display: flex; flex-direction: column; gap: 14px; }
.fa-section-head {
  font-size: 16px;
  font-weight: 600;
  letter-spacing: -0.01em;
  color: var(--ink-0);
  margin: 0;
  padding-bottom: 10px;
  border-bottom: 1px solid var(--rule-1);
}
.section-desc {
  font-size: 13.5px;
  line-height: 1.5;
  color: var(--ink-2);
  margin: 0;
}

/* Account meta — hairline rows */
.meta-rows { display: flex; flex-direction: column; }
.meta-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 11px 0;
  border-bottom: 1px solid var(--rule-1);
}
.meta-row:last-child { border-bottom: none; }
.meta-label { font-size: 13.5px; color: var(--ink-2); }
.meta-value { font-size: 13.5px; color: var(--ink-0); font-weight: 500; }
.meta-value.provider { display: inline-flex; align-items: center; gap: 6px; }
.provider-icon { width: 14px; height: 14px; }

/* Forms */
.fa-form { display: flex; flex-direction: column; gap: 16px; }
.field { display: flex; flex-direction: column; gap: 6px; }
.field-label { font-size: 13px; font-weight: 600; color: var(--ink-0); }
.field-hint {
  font-size: 12.5px;
  line-height: 1.5;
  color: var(--ink-2);
  margin: 0;
}

/* Flat inputs — paper-2 fill, hairline, accent focus ring */
.fa-input {
  width: 100%;
  height: 36px;
  padding: 0 11px;
  font-size: 14px;
  font-family: inherit;
  color: var(--ink-0);
  background: var(--paper-2);
  border: 1px solid var(--rule-1);
  border-radius: 8px;
  outline: none;
  transition: border-color .12s, box-shadow .12s;
}
.fa-input::placeholder { color: var(--ink-2); }
.fa-input:focus {
  border-color: var(--accent);
  box-shadow: 0 0 0 2px var(--accent-soft);
}
.fa-select {
  appearance: none;
  -webkit-appearance: none;
  background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='14' height='14' viewBox='0 0 24 24' fill='none' stroke='%238e8e93' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'%3E%3Cpath d='m6 9 6 6 6-6'/%3E%3C/svg%3E");
  background-repeat: no-repeat;
  background-position: right 10px center;
  padding-right: 30px;
}

/* Inline message banners — quiet, token-driven */
.msg {
  font-size: 13px;
  line-height: 1.45;
  padding: 9px 11px;
  border-radius: 8px;
  margin: 0;
}
.msg-ok {
  color: var(--c-good);
  background: color-mix(in oklab, var(--c-good) 10%, transparent);
  border: 1px solid color-mix(in oklab, var(--c-good) 28%, transparent);
}
.msg-err {
  color: var(--c-again);
  background: color-mix(in oklab, var(--c-again) 10%, transparent);
  border: 1px solid color-mix(in oklab, var(--c-again) 28%, transparent);
}

.loading-line { font-size: 13.5px; color: var(--ink-2); }

/* Inline link — accent as text */
.fa-inline-link { color: var(--accent-text); text-decoration: none; }
.fa-inline-link:hover { text-decoration: underline; }

/* Data section actions */
.data-actions { display: flex; gap: 10px; flex-wrap: wrap; }
.fa-btn-danger {
  color: var(--c-again);
  border-color: color-mix(in oklab, var(--c-again) 40%, var(--rule-1));
  background: transparent;
}
.fa-btn-danger:hover {
  background: color-mix(in oklab, var(--c-again) 10%, transparent);
  border-color: var(--c-again);
}

.self-start { align-self: flex-start; }
</style>
