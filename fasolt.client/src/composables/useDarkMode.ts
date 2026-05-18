import { ref, onMounted, onUnmounted } from 'vue'

const STORAGE_KEY = 'fasolt-theme'

export type ThemeMode = 'light' | 'dark' | 'auto'

// Module-level shared refs — every caller of useDarkMode sees the same state.
const mode = ref<ThemeMode>('auto')
const isDark = ref(false)

let mediaQuery: MediaQueryList | null = null
let initialized = false

function resolveAndApply() {
  let shouldBeDark: boolean
  if (mode.value === 'dark') shouldBeDark = true
  else if (mode.value === 'light') shouldBeDark = false
  else shouldBeDark = mediaQuery?.matches ?? false
  isDark.value = shouldBeDark
  document.documentElement.classList.toggle('dark', shouldBeDark)
}

function setMode(next: ThemeMode) {
  mode.value = next
  if (next === 'auto') localStorage.removeItem(STORAGE_KEY)
  else localStorage.setItem(STORAGE_KEY, next)
  resolveAndApply()
}

export function useDarkMode() {
  let handler: ((e: MediaQueryListEvent) => void) | null = null

  // Backwards-compat: cycle light → dark → auto.
  function toggle() {
    const next: ThemeMode =
      mode.value === 'light' ? 'dark' :
      mode.value === 'dark' ? 'auto' : 'light'
    setMode(next)
  }

  onMounted(() => {
    if (!initialized) {
      mediaQuery = window.matchMedia('(prefers-color-scheme: dark)')
      const stored = localStorage.getItem(STORAGE_KEY)
      mode.value = stored === 'dark' || stored === 'light' ? stored : 'auto'
      resolveAndApply()
      initialized = true
    }
    handler = () => { if (mode.value === 'auto') resolveAndApply() }
    mediaQuery?.addEventListener('change', handler)
  })

  onUnmounted(() => {
    if (mediaQuery && handler) {
      mediaQuery.removeEventListener('change', handler)
    }
  })

  return { isDark, mode, setMode, toggle }
}
