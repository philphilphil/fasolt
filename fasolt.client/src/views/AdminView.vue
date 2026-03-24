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
  cardCount: number
  deckCount: number
  isLockedOut: boolean
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
const lockDialogOpen = ref(false)
const lockTargetUser = ref<AdminUser | null>(null)
const errorMessage = ref<string | null>(null)

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

onMounted(fetchUsers)
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
            <TableHead>Email</TableHead>
            <TableHead>Display Name</TableHead>
            <TableHead class="text-right">Cards</TableHead>
            <TableHead class="text-right">Decks</TableHead>
            <TableHead>Status</TableHead>
            <TableHead class="text-right">Actions</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow v-if="isLoading">
            <TableCell :colspan="6" class="text-center text-muted-foreground">
              Loading...
            </TableCell>
          </TableRow>
          <TableRow v-else-if="users.length === 0">
            <TableCell :colspan="6" class="text-center text-muted-foreground">
              No users found.
            </TableCell>
          </TableRow>
          <TableRow v-for="u in users" :key="u.id">
            <TableCell class="font-medium">{{ u.email }}</TableCell>
            <TableCell>{{ u.displayName ?? '—' }}</TableCell>
            <TableCell class="text-right">{{ u.cardCount }}</TableCell>
            <TableCell class="text-right">{{ u.deckCount }}</TableCell>
            <TableCell>
              <Badge v-if="u.isLockedOut" variant="destructive">Locked</Badge>
              <Badge v-else variant="secondary">Active</Badge>
            </TableCell>
            <TableCell class="text-right">
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

    <!-- Lock confirmation dialog -->
    <Dialog v-model:open="lockDialogOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Lock user account?</DialogTitle>
          <DialogDescription>
            This will prevent {{ lockTargetUser?.email }} from logging in. You can unlock them later.
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
