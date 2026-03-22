import { ref, onMounted, onUnmounted } from 'vue'

const STORAGE_KEY = 'fasolt-theme'
type Theme = 'light' | 'dark' | 'system'

// Shared state across all component instances
const isDark = ref(false)
const theme = ref<Theme>('system')

export function useDarkMode() {
  let mediaQuery: MediaQueryList | null = null
  let handler: ((e: MediaQueryListEvent) => void) | null = null

  function apply() {
    const shouldBeDark =
      theme.value === 'dark' ||
      (theme.value === 'system' && (mediaQuery?.matches ?? false))
    isDark.value = shouldBeDark
    document.documentElement.classList.toggle('dark', shouldBeDark)
  }

  function toggle() {
    // Cycle: system -> dark -> light -> system
    const next: Record<Theme, Theme> = { system: 'dark', dark: 'light', light: 'system' }
    theme.value = next[theme.value]
    localStorage.setItem(STORAGE_KEY, theme.value)
    apply()
  }

  onMounted(() => {
    mediaQuery = window.matchMedia('(prefers-color-scheme: dark)')
    const stored = localStorage.getItem(STORAGE_KEY) as Theme | null
    if (stored && ['light', 'dark', 'system'].includes(stored)) {
      theme.value = stored
    }

    handler = () => apply()
    mediaQuery.addEventListener('change', handler)
    apply()
  })

  onUnmounted(() => {
    if (mediaQuery && handler) {
      mediaQuery.removeEventListener('change', handler)
    }
  })

  return { isDark, theme, toggle }
}
