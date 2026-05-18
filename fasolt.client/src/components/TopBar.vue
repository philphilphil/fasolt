<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted } from 'vue'
import { Input } from '@/components/ui/input'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import SearchResults from '@/components/SearchResults.vue'
import FasoltWordmark from '@/components/FasoltWordmark.vue'
import { useAuthStore } from '@/stores/auth'
import { useSearch } from '@/composables/useSearch'
import { useDarkMode } from '@/composables/useDarkMode'

const auth = useAuthStore()
const { isDark, toggle } = useDarkMode()
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
const userInitial = computed(() => userLabel.value ? userLabel.value[0].toUpperCase() : '?')

function focusSearch() {
  const el = searchInputRef.value?.$el as HTMLInputElement | undefined
  el?.focus()
}

function handleClickOutside(e: MouseEvent) {
  if (searchContainerRef.value && !searchContainerRef.value.contains(e.target as Node)) close()
}

function handleGlobalKeyDown(e: KeyboardEvent) {
  if ((e.metaKey || e.ctrlKey) && e.key === 'k') { e.preventDefault(); focusSearch() }
}

onMounted(() => {
  document.addEventListener('keydown', handleGlobalKeyDown)
  document.addEventListener('click', handleClickOutside)
})
onUnmounted(() => {
  document.removeEventListener('keydown', handleGlobalKeyDown)
  document.removeEventListener('click', handleClickOutside)
})

async function handleLogout() { await auth.logout(); window.location.href = '/' }
</script>

<template>
  <header class="topbar">
    <RouterLink to="/study" class="brand">
      <FasoltWordmark />
    </RouterLink>

    <div ref="searchContainerRef" class="search-wrap">
      <div class="search-shell">
        <svg class="search-icon" xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="7"/><path d="m20 20-3.5-3.5"/></svg>
        <Input
          ref="searchInputRef"
          v-model="query"
          type="text"
          placeholder="Search cards, decks, sources…"
          class="search-input"
          @keydown="onKeyDown"
        />
        <div v-if="!isOpen" class="search-kbd">
          <span class="fa-kbd">⌘</span>
          <span class="fa-kbd">K</span>
        </div>
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

    <div class="topbar-right">
      <button class="theme-btn" :aria-label="isDark ? 'Switch to light' : 'Switch to dark'" @click="toggle">
        <svg v-if="isDark" xmlns="http://www.w3.org/2000/svg" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41"/></svg>
        <svg v-else xmlns="http://www.w3.org/2000/svg" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z"/></svg>
      </button>
      <div class="topbar-divider" />
      <DropdownMenu>
        <DropdownMenuTrigger as-child>
          <button class="user-chip" :aria-label="`User menu for ${userLabel}`">
            <span class="user-avatar">{{ userInitial }}</span>
            <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"><path d="m6 9 6 6 6-6"/></svg>
          </button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" class="w-48">
          <div class="px-2 py-1.5 text-xs text-ink-2 truncate">{{ userLabel }}</div>
          <DropdownMenuSeparator />
          <DropdownMenuItem as-child>
            <RouterLink to="/settings" class="cursor-pointer">Settings</RouterLink>
          </DropdownMenuItem>
          <DropdownMenuSeparator />
          <DropdownMenuItem class="cursor-pointer" @click="handleLogout">Log out</DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  </header>
</template>

<style scoped>
.topbar {
  display: flex;
  align-items: center;
  height: 52px;
  padding: 0 20px;
  gap: 16px;
  background: var(--paper-0);
  border-bottom: 1px solid var(--rule-1);
}
.brand {
  flex: 0 0 auto;
  display: flex;
  align-items: center;
  text-decoration: none;
}
.search-wrap {
  flex: 1 1 auto;
  max-width: 520px;
  margin: 0 auto;
  position: relative;
}
.search-shell {
  display: flex;
  align-items: center;
  gap: 10px;
  height: 32px;
  padding: 0 12px;
  border: 1px solid var(--rule-1);
  border-radius: 8px;
  background: var(--paper-1);
  color: var(--ink-2);
  transition: border-color .12s, background .12s;
}
.search-shell:focus-within {
  border-color: var(--accent);
  background: var(--paper-0);
}
.search-icon { flex-shrink: 0; }
.search-input {
  flex: 1;
  border: none !important;
  outline: none !important;
  background: transparent !important;
  color: var(--ink-0) !important;
  font-size: 13px !important;
  height: 30px !important;
  padding: 0 !important;
  box-shadow: none !important;
}
.search-kbd {
  margin-left: auto;
  display: flex;
  gap: 3px;
  flex-shrink: 0;
}
.topbar-right {
  flex: 0 0 auto;
  display: flex;
  align-items: center;
  gap: 8px;
}
.theme-btn {
  width: 32px;
  height: 32px;
  display: grid;
  place-items: center;
  background: transparent;
  border: 1px solid transparent;
  border-radius: 6px;
  color: var(--ink-1);
  cursor: pointer;
  transition: background .12s, border-color .12s, color .12s;
}
.theme-btn:hover {
  background: var(--paper-1);
  border-color: var(--rule-1);
  color: var(--ink-0);
}
.topbar-divider {
  width: 1px;
  height: 18px;
  background: var(--rule-1);
}
.user-chip {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 0 4px 0 4px;
  height: 32px;
  border-radius: 6px;
  border: 1px solid var(--rule-1);
  background: var(--paper-1);
  color: var(--ink-2);
  cursor: pointer;
  transition: background .12s, border-color .12s;
}
.user-chip:hover {
  background: var(--paper-2);
  border-color: var(--ink-3);
}
.user-avatar {
  width: 24px;
  height: 24px;
  border-radius: 4px;
  background: var(--accent);
  color: var(--accent-on);
  display: grid;
  place-items: center;
  font-family: 'Geist Mono', monospace;
  font-size: 11px;
  font-weight: 600;
}
</style>
