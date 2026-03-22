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
import { useDarkMode } from '@/composables/useDarkMode'
import { useAuthStore } from '@/stores/auth'
import { useSearch } from '@/composables/useSearch'

const { theme, toggle } = useDarkMode()
const auth = useAuthStore()
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
  onKeyDown,
  navigateToResult,
  close,
} = useSearch()

const themeIcon = computed(() => {
  if (theme.value === 'dark') return '☾'
  if (theme.value === 'light') return '☀'
  return '◑'
})

const themeLabel = computed(() => {
  if (theme.value === 'dark') return 'Dark'
  if (theme.value === 'light') return 'Light'
  return 'System'
})

const userInitial = computed(() => {
  if (auth.user?.displayName) return auth.user.displayName[0].toUpperCase()
  if (auth.user?.email) return auth.user.email[0].toUpperCase()
  return '?'
})

const userLabel = computed(() => auth.user?.displayName || auth.user?.email || '')

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
    <span class="flex items-center gap-2 font-mono text-[13px] font-bold text-foreground tracking-tight">
      <img src="/logo.png" alt="fasolt" class="h-7 object-contain" style="image-rendering: pixelated" />
      fasolt
    </span>
    <div ref="searchContainerRef" class="relative hidden sm:block">
      <Input
        ref="searchInputRef"
        v-model="query"
        type="text"
        placeholder="Search cards and decks…"
        class="h-8 w-[260px] bg-secondary pl-8 text-xs"
        @keydown="onKeyDown"
      />
      <div class="pointer-events-none absolute left-2.5 top-1/2 -translate-y-1/2 text-muted-foreground">
        <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/></svg>
      </div>
      <div v-if="!isOpen" class="pointer-events-none absolute right-2 top-1/2 -translate-y-1/2">
        <kbd class="rounded border border-border bg-muted px-1.5 py-0.5 text-[10px] text-muted-foreground">⌘K</kbd>
      </div>
      <SearchResults
        v-if="isOpen"
        :results="results"
        :is-loading="isLoading"
        :active-index="activeIndex"
        :flat-items="flatItems"
        :has-results="hasResults"
        @select="navigateToResult"
        @update:active-index="activeIndex = $event"
      />
    </div>
    <div class="flex items-center gap-2">
      <Button
        variant="ghost"
        size="sm"
        class="h-8 gap-1.5 text-xs text-muted-foreground"
        :aria-label="`Theme: ${themeLabel}. Click to change.`"
        @click="toggle"
      >
        <span class="text-sm">{{ themeIcon }}</span>
        <span class="hidden sm:inline">{{ themeLabel }}</span>
      </Button>
      <DropdownMenu>
        <DropdownMenuTrigger as-child>
          <Button
            variant="ghost"
            size="sm"
            class="h-8 w-8 rounded-full bg-secondary p-0 text-xs font-medium"
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
