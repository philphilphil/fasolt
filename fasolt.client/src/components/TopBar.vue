<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import SearchResults from '@/components/SearchResults.vue'
import { useAuthStore } from '@/stores/auth'
import { useSearch } from '@/composables/useSearch'
import { useDarkMode } from '@/composables/useDarkMode'

const auth = useAuthStore()
const { isDark, toggle } = useDarkMode()
const router = useRouter()
const searchInputRef = ref<InstanceType<typeof Input> | null>(null)
const searchContainerRef = ref<HTMLDivElement | null>(null)

const {
  query,
  results,
  isLoading,
  isOpen,
  activeIndex,
  flatItems,
  hasResults,
  error,
  onKeyDown,
  navigateToResult,
  close,
} = useSearch()

const userLabel = computed(() => auth.user?.displayName || auth.user?.email || '')

const userInitial = computed(() => {
  if (userLabel.value) return userLabel.value[0].toUpperCase()
  return '?'
})

function focusSearch() {
  const el = searchInputRef.value?.$el as HTMLInputElement | undefined
  el?.focus()
}

function handleClickOutside(e: MouseEvent) {
  if (searchContainerRef.value && !searchContainerRef.value.contains(e.target as Node)) {
    close()
  }
}

function handleGlobalKeyDown(e: KeyboardEvent) {
  if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
    e.preventDefault()
    focusSearch()
  }
}

onMounted(() => {
  document.addEventListener('keydown', handleGlobalKeyDown)
  document.addEventListener('click', handleClickOutside)
})

onUnmounted(() => {
  document.removeEventListener('keydown', handleGlobalKeyDown)
  document.removeEventListener('click', handleClickOutside)
})

async function handleLogout() {
  await auth.logout()
  router.push('/login')
}
</script>

<template>
  <header class="flex items-center justify-between border-b border-border px-5 py-3">
    <span class="flex items-center gap-2.5 text-[13px] font-bold text-foreground tracking-tight">
      <img src="/logo.svg" alt="fasolt" class="h-9 object-contain" />
      fasolt
    </span>
    <div ref="searchContainerRef" class="relative hidden sm:block">
      <Input
        ref="searchInputRef"
        v-model="query"
        type="text"
        placeholder="Search cards and decks..."
        class="h-8 w-[260px] bg-secondary pl-8 text-xs focus:border-accent"
        @keydown="onKeyDown"
      />
      <div class="pointer-events-none absolute left-2.5 top-1/2 -translate-y-1/2 text-muted-foreground">
        <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/></svg>
      </div>
      <div v-if="!isOpen" class="pointer-events-none absolute right-2 top-1/2 -translate-y-1/2">
        <kbd class="rounded border border-border bg-muted px-1.5 py-0.5 text-[10px] text-muted-foreground">⌘K</kbd>
      </div>
      <SearchResults
        v-if="isOpen"
        :results="results"
        :query="query"
        :is-loading="isLoading"
        :active-index="activeIndex"
        :flat-items="flatItems"
        :has-results="hasResults"
        :error="error"
        @select="navigateToResult"
        @update:active-index="activeIndex = $event"
      />
    </div>
    <div class="flex items-center gap-1.5">
      <Button
        variant="ghost"
        size="sm"
        class="h-8 w-8 p-0"
        aria-label="Toggle dark mode"
        @click="toggle"
      >
        <svg v-if="isDark" xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="5"/><line x1="12" y1="1" x2="12" y2="3"/><line x1="12" y1="21" x2="12" y2="23"/><line x1="4.22" y1="4.22" x2="5.64" y2="5.64"/><line x1="18.36" y1="18.36" x2="19.78" y2="19.78"/><line x1="1" y1="12" x2="3" y2="12"/><line x1="21" y1="12" x2="23" y2="12"/><line x1="4.22" y1="19.78" x2="5.64" y2="18.36"/><line x1="18.36" y1="5.64" x2="19.78" y2="4.22"/></svg>
        <svg v-else xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/></svg>
      </Button>
      <DropdownMenu>
        <DropdownMenuTrigger as-child>
          <Button
            variant="ghost"
            size="sm"
            class="h-8 w-8 rounded bg-secondary p-0 text-xs font-medium"
            :aria-label="`User menu for ${userLabel}`"
          >
            {{ userInitial }}
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" class="w-48">
          <div class="px-2 py-1.5 text-xs text-muted-foreground truncate">
            {{ userLabel }}
          </div>
          <DropdownMenuSeparator />
          <DropdownMenuItem as-child>
            <RouterLink to="/settings" class="cursor-pointer">Settings</RouterLink>
          </DropdownMenuItem>
          <DropdownMenuSeparator />
          <DropdownMenuItem class="cursor-pointer" @click="handleLogout">
            Log out
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  </header>
</template>
