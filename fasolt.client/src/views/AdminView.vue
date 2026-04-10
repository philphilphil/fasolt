<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { apiFetch } from '@/api/client'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/components/ui/dialog'

interface AdminUser {
  id: string
  email: string
  displayName: string | null
  externalProvider: string | null
  cardCount: number
  deckCount: number
  isLockedOut: boolean
  hasPush: boolean
  emailConfirmed: boolean
}

interface AdminUserListResponse {
  users: AdminUser[]
  totalCount: number
  page: number
  pageSize: number
}

interface LogEntry {
  id: number
  type: string
  message: string
  detail: string | null
  success: boolean
  createdAt: string
}

interface LogListResponse {
  logs: LogEntry[]
  totalCount: number
  page: number
  pageSize: number
}

const users = ref<AdminUser[]>([])
const totalCount = ref(0)
const page = ref(1)
const pageSize = 50
const isLoading = ref(false)
const lockDialogOpen = ref(false)
const lockTargetUser = ref<AdminUser | null>(null)
const errorMessage = ref<string | null>(null)

const logs = ref<LogEntry[]>([])
const logsTotal = ref(0)
const logsPage = ref(1)
const logsLoading = ref(false)
const pushingUserId = ref<string | null>(null)

async function fetchUsers() {
  isLoading.value = true
  try {
    const data = await apiFetch<AdminUserListResponse>(
      `/admin/users?page=${page.value}&pageSize=${pageSize}`,
    )
    users.value = data.users
    totalCount.value = data.totalCount
  } finally {
    isLoading.value = false
  }
}

function confirmLock(user: AdminUser) {
  lockTargetUser.value = user
  lockDialogOpen.value = true
}

async function lockUser() {
  if (!lockTargetUser.value) return
  try {
    await apiFetch(`/admin/users/${lockTargetUser.value.id}/lock`, { method: 'POST' })
    lockDialogOpen.value = false
    await fetchUsers()
  } catch (e: any) {
    errorMessage.value = e.message ?? 'Failed to lock user'
    console.error('Failed to lock user', e)
  }
}

async function unlockUser(id: string) {
  try {
    await apiFetch(`/admin/users/${id}/unlock`, { method: 'POST' })
    await fetchUsers()
  } catch (e: any) {
    errorMessage.value = e.message ?? 'Failed to unlock user'
    console.error('Failed to unlock user', e)
  }
}

const totalPages = () => Math.ceil(totalCount.value / pageSize)

function nextPage() {
  if (page.value < totalPages()) {
    page.value++
    fetchUsers()
  }
}

function prevPage() {
  if (page.value > 1) {
    page.value--
    fetchUsers()
  }
}

async function pushToUser(user: AdminUser) {
  pushingUserId.value = user.id
  try {
    await apiFetch<{ message: string }>(`/admin/users/${user.id}/push`, { method: 'POST' })
    errorMessage.value = null
    await fetchLogs()
  } catch (e: any) {
    errorMessage.value = e.message ?? 'Failed to send push'
  } finally {
    pushingUserId.value = null
  }
}

async function fetchLogs() {
  logsLoading.value = true
  try {
    const data = await apiFetch<LogListResponse>(
      `/admin/logs?page=${logsPage.value}&pageSize=${pageSize}`,
    )
    logs.value = data.logs
    logsTotal.value = data.totalCount
  } finally {
    logsLoading.value = false
  }
}

const logsTotalPages = () => Math.ceil(logsTotal.value / pageSize)

function logsNextPage() {
  if (logsPage.value < logsTotalPages()) {
    logsPage.value++
    fetchLogs()
  }
}

function logsPrevPage() {
  if (logsPage.value > 1) {
    logsPage.value--
    fetchLogs()
  }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleString()
}

onMounted(() => {
  fetchUsers()
  fetchLogs()
})
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="text-2xl font-bold tracking-tight">Admin</h1>
      <p class="text-muted-foreground">Manage users and monitor usage.</p>
    </div>

    <div v-if="errorMessage" class="rounded-md border border-destructive bg-destructive/10 px-4 py-3 text-sm text-destructive">
      {{ errorMessage }}
      <button class="ml-2 underline" @click="errorMessage = null">Dismiss</button>
    </div>

    <div class="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>User</TableHead>
            <TableHead>Provider</TableHead>
            <TableHead class="text-right">Cards</TableHead>
            <TableHead class="text-right">Decks</TableHead>
            <TableHead>Push</TableHead>
            <TableHead>Status</TableHead>
            <TableHead class="text-right">Actions</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow v-if="isLoading">
            <TableCell :colspan="7" class="text-center text-muted-foreground">
              Loading...
            </TableCell>
          </TableRow>
          <TableRow v-else-if="users.length === 0">
            <TableCell :colspan="7" class="text-center text-muted-foreground">
              No users found.
            </TableCell>
          </TableRow>
          <TableRow v-for="u in users" :key="u.id">
            <TableCell class="font-medium">
              <span class="inline-flex items-center gap-2">
                {{ u.displayName || u.email }}
                <span
                  v-if="u.emailConfirmed"
                  class="text-success"
                  title="Email confirmed"
                  aria-label="Email confirmed"
                >&#x2713;</span>
                <span
                  v-else
                  class="text-muted-foreground"
                  title="Email not confirmed"
                  aria-label="Email not confirmed"
                >&#x25CB;</span>
              </span>
            </TableCell>
            <TableCell>
              <Badge v-if="u.externalProvider" variant="secondary">{{ u.externalProvider }}</Badge>
              <span v-else class="text-xs text-muted-foreground">Email</span>
            </TableCell>
            <TableCell class="text-right">{{ u.cardCount }}</TableCell>
            <TableCell class="text-right">{{ u.deckCount }}</TableCell>
            <TableCell>
              <Badge v-if="u.hasPush" variant="secondary">Yes</Badge>
              <span v-else class="text-muted-foreground">—</span>
            </TableCell>
            <TableCell>
              <Badge v-if="u.isLockedOut" variant="destructive">Locked</Badge>
              <Badge v-else variant="secondary">Active</Badge>
            </TableCell>
            <TableCell class="text-right flex gap-1 justify-end">
              <Button v-if="u.hasPush" variant="outline" size="sm" :disabled="pushingUserId === u.id" @click="pushToUser(u)">
                {{ pushingUserId === u.id ? 'Sending...' : 'Push' }}
              </Button>
              <Button v-if="!u.isLockedOut" variant="destructive" size="sm" @click="confirmLock(u)">
                Lock
              </Button>
              <Button v-else variant="outline" size="sm" @click="unlockUser(u.id)">
                Unlock
              </Button>
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </div>

    <div v-if="totalPages() > 1" class="flex items-center justify-between">
      <p class="text-sm text-muted-foreground">
        Page {{ page }} of {{ totalPages() }} ({{ totalCount }} users)
      </p>
      <div class="flex gap-2">
        <Button variant="outline" size="sm" :disabled="page <= 1" @click="prevPage">
          Previous
        </Button>
        <Button variant="outline" size="sm" :disabled="page >= totalPages()" @click="nextPage">
          Next
        </Button>
      </div>
    </div>

    <!-- Logs -->
    <div class="pt-4 flex items-center justify-between">
      <div>
        <h2 class="text-lg font-semibold tracking-tight">Logs</h2>
        <p class="text-sm text-muted-foreground">Recent system activity.</p>
      </div>
      <Button variant="outline" size="sm" :disabled="logsLoading" @click="fetchLogs">
        Refresh
      </Button>
    </div>

    <div class="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Time</TableHead>
            <TableHead>Type</TableHead>
            <TableHead>Message</TableHead>
            <TableHead>Detail</TableHead>
            <TableHead>Status</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow v-if="logsLoading">
            <TableCell :colspan="5" class="text-center text-muted-foreground">
              Loading...
            </TableCell>
          </TableRow>
          <TableRow v-else-if="logs.length === 0">
            <TableCell :colspan="5" class="text-center text-muted-foreground">
              No logs yet.
            </TableCell>
          </TableRow>
          <TableRow v-for="l in logs" :key="l.id">
            <TableCell class="whitespace-nowrap text-sm">{{ formatDate(l.createdAt) }}</TableCell>
            <TableCell><Badge variant="outline">{{ l.type }}</Badge></TableCell>
            <TableCell>{{ l.message }}</TableCell>
            <TableCell class="text-muted-foreground text-sm">{{ l.detail ?? '—' }}</TableCell>
            <TableCell>
              <Badge v-if="l.success" variant="secondary">OK</Badge>
              <Badge v-else variant="destructive">Error</Badge>
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </div>

    <div v-if="logsTotalPages() > 1" class="flex items-center justify-between">
      <p class="text-sm text-muted-foreground">
        Page {{ logsPage }} of {{ logsTotalPages() }} ({{ logsTotal }} entries)
      </p>
      <div class="flex gap-2">
        <Button variant="outline" size="sm" :disabled="logsPage <= 1" @click="logsPrevPage">
          Previous
        </Button>
        <Button variant="outline" size="sm" :disabled="logsPage >= logsTotalPages()" @click="logsNextPage">
          Next
        </Button>
      </div>
    </div>

    <!-- Lock confirmation dialog -->
    <Dialog v-model:open="lockDialogOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Lock user account?</DialogTitle>
          <DialogDescription>
            This will prevent {{ lockTargetUser?.displayName || lockTargetUser?.email }} from logging in. You can unlock them later.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter class="gap-2">
          <Button variant="outline" size="sm" @click="lockDialogOpen = false">Cancel</Button>
          <Button variant="destructive" size="sm" @click="lockUser">Lock Account</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
