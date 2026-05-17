<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
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
import { Input } from '@/components/ui/input'
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

const users = ref<AdminUser[]>([])
const totalCount = ref(0)
const page = ref(1)
const pageSize = 50
const isLoading = ref(false)
const errorMessage = ref<string | null>(null)

const search = ref('')
const providerFilter = ref<string>('')
const lockedOnly = ref(false)
const hasPushOnly = ref(false)
let searchDebounce: number | undefined

const lockDialogOpen = ref(false)
const lockTargetUser = ref<AdminUser | null>(null)
const deleteDialogOpen = ref(false)
const deleteTargetUser = ref<AdminUser | null>(null)
const pushingUserId = ref<string | null>(null)

async function fetchUsers() {
  isLoading.value = true
  try {
    const params = new URLSearchParams({
      page: String(page.value),
      pageSize: String(pageSize),
    })
    if (search.value.trim()) params.set('q', search.value.trim())
    if (providerFilter.value) params.set('provider', providerFilter.value)
    if (lockedOnly.value) params.set('lockedOnly', 'true')
    if (hasPushOnly.value) params.set('hasPushOnly', 'true')
    const data = await apiFetch<AdminUserListResponse>(`/admin/users?${params.toString()}`)
    users.value = data.users
    totalCount.value = data.totalCount
  } catch (e: any) {
    errorMessage.value = e.message ?? 'Failed to load users'
  } finally {
    isLoading.value = false
  }
}

watch([providerFilter, lockedOnly, hasPushOnly], () => {
  page.value = 1
  fetchUsers()
})

watch(search, () => {
  if (searchDebounce) window.clearTimeout(searchDebounce)
  searchDebounce = window.setTimeout(() => {
    page.value = 1
    fetchUsers()
  }, 250)
})

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
  }
}

async function unlockUser(id: string) {
  try {
    await apiFetch(`/admin/users/${id}/unlock`, { method: 'POST' })
    await fetchUsers()
  } catch (e: any) {
    errorMessage.value = e.message ?? 'Failed to unlock user'
  }
}

function confirmDelete(user: AdminUser) {
  deleteTargetUser.value = user
  deleteDialogOpen.value = true
}

async function deleteUser() {
  if (!deleteTargetUser.value) return
  try {
    await apiFetch(`/admin/users/${deleteTargetUser.value.id}`, { method: 'DELETE' })
    deleteDialogOpen.value = false
    await fetchUsers()
  } catch (e: any) {
    errorMessage.value = e.message ?? 'Failed to delete user'
  }
}

async function pushToUser(user: AdminUser) {
  pushingUserId.value = user.id
  try {
    await apiFetch<{ message: string }>(`/admin/users/${user.id}/push`, { method: 'POST' })
    errorMessage.value = null
  } catch (e: any) {
    errorMessage.value = e.message ?? 'Failed to send push'
  } finally {
    pushingUserId.value = null
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

function clearFilters() {
  search.value = ''
  providerFilter.value = ''
  lockedOnly.value = false
  hasPushOnly.value = false
}

const hasActiveFilters = () =>
  !!(search.value || providerFilter.value || lockedOnly.value || hasPushOnly.value)

onMounted(fetchUsers)
</script>

<template>
  <div class="space-y-4">
    <div class="flex items-center justify-between">
      <div>
        <h2 class="text-lg font-semibold tracking-tight">Users</h2>
        <p class="text-sm text-muted-foreground">{{ totalCount }} total</p>
      </div>
    </div>

    <div v-if="errorMessage" class="rounded-md border border-destructive bg-destructive/10 px-4 py-3 text-sm text-destructive">
      {{ errorMessage }}
      <button class="ml-2 underline" @click="errorMessage = null">Dismiss</button>
    </div>

    <div class="flex flex-wrap items-end gap-3">
      <div class="flex flex-col gap-1 flex-1 min-w-[200px]">
        <label class="text-xs font-medium text-muted-foreground" for="user-search">Search</label>
        <Input
          id="user-search"
          v-model="search"
          type="search"
          placeholder="Email or username..."
        />
      </div>
      <div class="flex flex-col gap-1">
        <label class="text-xs font-medium text-muted-foreground" for="user-provider">Provider</label>
        <select
          id="user-provider"
          v-model="providerFilter"
          class="h-10 rounded-md border border-input bg-background px-3 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          <option value="">All</option>
          <option value="Email">Email</option>
          <option value="GitHub">GitHub</option>
          <option value="Apple">Apple</option>
        </select>
      </div>
      <label class="flex h-10 items-center gap-2 text-sm">
        <input v-model="lockedOnly" type="checkbox" class="h-4 w-4 rounded border-input" />
        Locked only
      </label>
      <label class="flex h-10 items-center gap-2 text-sm">
        <input v-model="hasPushOnly" type="checkbox" class="h-4 w-4 rounded border-input" />
        Push enabled
      </label>
      <Button v-if="hasActiveFilters()" variant="ghost" size="sm" @click="clearFilters">Clear</Button>
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
            <TableCell :colspan="7" class="text-center text-muted-foreground">Loading…</TableCell>
          </TableRow>
          <TableRow v-else-if="users.length === 0">
            <TableCell :colspan="7" class="text-center text-muted-foreground">No users match the current filters.</TableCell>
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
            <TableCell class="text-right tabular-nums">{{ u.cardCount }}</TableCell>
            <TableCell class="text-right tabular-nums">{{ u.deckCount }}</TableCell>
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
                {{ pushingUserId === u.id ? 'Sending…' : 'Push' }}
              </Button>
              <Button v-if="!u.isLockedOut" variant="destructive" size="sm" @click="confirmLock(u)">Lock</Button>
              <Button v-else variant="outline" size="sm" @click="unlockUser(u.id)">Unlock</Button>
              <Button variant="destructive" size="sm" @click="confirmDelete(u)">Delete</Button>
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </div>

    <div v-if="totalPages() > 1" class="flex items-center justify-between">
      <p class="text-sm text-muted-foreground">
        Page {{ page }} of {{ totalPages() }}
      </p>
      <div class="flex gap-2">
        <Button variant="outline" size="sm" :disabled="page <= 1" @click="prevPage">Previous</Button>
        <Button variant="outline" size="sm" :disabled="page >= totalPages()" @click="nextPage">Next</Button>
      </div>
    </div>

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

    <Dialog v-model:open="deleteDialogOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Delete user account?</DialogTitle>
          <DialogDescription>
            This will permanently delete {{ deleteTargetUser?.displayName || deleteTargetUser?.email }} and all their data including cards, decks, review history, and notification tokens. This action cannot be undone.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter class="gap-2">
          <Button variant="outline" size="sm" @click="deleteDialogOpen = false">Cancel</Button>
          <Button variant="destructive" size="sm" @click="deleteUser">Delete Account</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
