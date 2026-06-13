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

const logs = ref<LogEntry[]>([])
const totalCount = ref(0)
const page = ref(1)
const pageSize = 50
const isLoading = ref(false)
const errorMessage = ref<string | null>(null)

const typeFilter = ref<string>('')
const successFilter = ref<string>('')
const searchQuery = ref<string>('')
let searchDebounce: number | undefined

async function fetchLogs() {
  isLoading.value = true
  try {
    const params = new URLSearchParams({
      page: String(page.value),
      pageSize: String(pageSize),
    })
    if (typeFilter.value) params.set('type', typeFilter.value)
    if (successFilter.value) params.set('success', successFilter.value)
    if (searchQuery.value.trim()) params.set('q', searchQuery.value.trim())
    const data = await apiFetch<LogListResponse>(`/admin/logs?${params.toString()}`)
    logs.value = data.logs
    totalCount.value = data.totalCount
  } catch (e: any) {
    errorMessage.value = e.message ?? 'Failed to load logs'
  } finally {
    isLoading.value = false
  }
}

watch([typeFilter, successFilter], () => {
  page.value = 1
  fetchLogs()
})

watch(searchQuery, () => {
  if (searchDebounce) window.clearTimeout(searchDebounce)
  searchDebounce = window.setTimeout(() => {
    page.value = 1
    fetchLogs()
  }, 250)
})

const totalPages = () => Math.ceil(totalCount.value / pageSize)

function nextPage() {
  if (page.value < totalPages()) {
    page.value++
    fetchLogs()
  }
}

function prevPage() {
  if (page.value > 1) {
    page.value--
    fetchLogs()
  }
}

function clearFilters() {
  typeFilter.value = ''
  successFilter.value = ''
  searchQuery.value = ''
}

const hasActiveFilters = () =>
  !!(typeFilter.value || successFilter.value || searchQuery.value)

function formatDate(iso: string) {
  return new Date(iso).toLocaleString()
}

function typeLabel(t: string) {
  switch (t) {
    case 'UserRegistered': return 'Sign-up'
    case 'Notification': return 'Notification'
    case 'Admin': return 'Admin'
    default: return t
  }
}

onMounted(fetchLogs)
</script>

<template>
  <div class="space-y-4">
    <div class="flex items-center justify-between">
      <div>
        <h2 class="text-lg font-semibold tracking-tight text-ink-0">Activity</h2>
        <p class="text-sm text-ink-2">{{ totalCount }} entries</p>
      </div>
      <button class="fa-btn" :disabled="isLoading" @click="fetchLogs">Refresh</button>
    </div>

    <div v-if="errorMessage" class="admin-error">
      {{ errorMessage }}
      <button class="ml-2 underline" @click="errorMessage = null">Dismiss</button>
    </div>

    <div class="flex flex-wrap items-end gap-3">
      <div class="flex flex-col gap-1.5">
        <label class="fa-cap" for="log-type-filter">Type</label>
        <select
          id="log-type-filter"
          v-model="typeFilter"
          class="admin-select"
        >
          <option value="">All types</option>
          <option value="UserRegistered">Sign-ups</option>
          <option value="Notification">Notifications</option>
          <option value="Admin">Admin actions</option>
        </select>
      </div>
      <div class="flex flex-col gap-1.5">
        <label class="fa-cap" for="log-status-filter">Status</label>
        <select
          id="log-status-filter"
          v-model="successFilter"
          class="admin-select"
        >
          <option value="">All</option>
          <option value="true">OK</option>
          <option value="false">Errors</option>
        </select>
      </div>
      <div class="flex flex-col gap-1.5 flex-1 min-w-[200px]">
        <label class="fa-cap" for="log-search">Search</label>
        <Input
          id="log-search"
          v-model="searchQuery"
          type="search"
          placeholder="Search message or detail..."
        />
      </div>
      <Button v-if="hasActiveFilters()" variant="ghost" size="sm" @click="clearFilters">Clear</Button>
    </div>

    <div class="fa-surface overflow-hidden">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead class="whitespace-nowrap">Time</TableHead>
            <TableHead>Type</TableHead>
            <TableHead>Message</TableHead>
            <TableHead>Detail</TableHead>
            <TableHead>Status</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow v-if="isLoading">
            <TableCell :colspan="5" class="text-center text-ink-2">Loading…</TableCell>
          </TableRow>
          <TableRow v-else-if="logs.length === 0">
            <TableCell :colspan="5" class="text-center text-ink-2">No entries match the current filters.</TableCell>
          </TableRow>
          <TableRow v-for="l in logs" :key="l.id">
            <TableCell class="whitespace-nowrap text-sm text-ink-1">{{ formatDate(l.createdAt) }}</TableCell>
            <TableCell><Badge variant="outline">{{ typeLabel(l.type) }}</Badge></TableCell>
            <TableCell>{{ l.message }}</TableCell>
            <TableCell class="text-ink-2 text-sm">{{ l.detail ?? '—' }}</TableCell>
            <TableCell>
              <Badge v-if="l.success" variant="secondary">OK</Badge>
              <Badge v-else variant="destructive">Error</Badge>
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </div>

    <div v-if="totalPages() > 1" class="flex items-center justify-between">
      <p class="text-sm text-ink-2">
        Page {{ page }} of {{ totalPages() }}
      </p>
      <div class="flex gap-2">
        <Button variant="outline" size="sm" :disabled="page <= 1" @click="prevPage">Previous</Button>
        <Button variant="outline" size="sm" :disabled="page >= totalPages()" @click="nextPage">Next</Button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.admin-error {
  border: 1px solid var(--c-again);
  border-radius: 9px;
  padding: 10px 14px;
  font-size: 13px;
  color: var(--c-again);
}
.admin-select {
  height: 36px;
  border-radius: 9px;
  border: 1px solid var(--rule-1);
  background: var(--paper-1);
  color: var(--ink-0);
  padding: 0 12px;
  font-size: 13px;
}
.admin-select:focus-visible {
  outline: none;
  border-color: var(--accent);
}
</style>
