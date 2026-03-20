<script setup lang="ts">
import { computed } from 'vue'
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
import KbdHint from '@/components/KbdHint.vue'
import { useDarkMode } from '@/composables/useDarkMode'
import { useAuthStore } from '@/stores/auth'

const { theme, toggle } = useDarkMode()
const auth = useAuthStore()
const router = useRouter()

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

async function handleLogout() {
  await auth.logout()
  router.push('/login')
}
</script>

<template>
  <header class="flex items-center justify-between border-b border-border px-5 py-3">
    <span class="font-mono text-[13px] font-bold text-foreground tracking-tight">
      spaced-md
    </span>
    <div class="relative hidden sm:block">
      <Input
        type="text"
        placeholder="Search cards, files…"
        class="h-8 w-[200px] bg-secondary pl-8 text-xs"
        readonly
      />
      <div class="absolute left-2 top-1/2 -translate-y-1/2">
        <KbdHint keys="⌘K" />
      </div>
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
