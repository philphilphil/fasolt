import { ref, onMounted, onUnmounted } from 'vue'

const STORAGE_KEY = 'fasolt-theme'

const isDark = ref(false)

export function useDarkMode() {
  let mediaQuery: MediaQueryList | null = null
  let handler: ((e: MediaQueryListEvent) => void) | null = null

  function apply() {
    const stored = localStorage.getItem(STORAGE_KEY)
    let shouldBeDark: boolean
    if (stored === 'dark') {
      shouldBeDark = true
    } else if (stored === 'light') {
      shouldBeDark = false
    } else {
      shouldBeDark = mediaQuery?.matches ?? false
    }
    isDark.value = shouldBeDark
    document.documentElement.classList.toggle('dark', shouldBeDark)
  }

  function toggle() {
    const newTheme = isDark.value ? 'light' : 'dark'
    localStorage.setItem(STORAGE_KEY, newTheme)
    apply()
  }

  onMounted(() => {
    mediaQuery = window.matchMedia('(prefers-color-scheme: dark)')
    handler = () => apply()
    mediaQuery.addEventListener('change', handler)
    apply()
  })

  onUnmounted(() => {
    if (mediaQuery && handler) {
      mediaQuery.removeEventListener('change', handler)
    }
  })

  return { isDark, toggle }
}
