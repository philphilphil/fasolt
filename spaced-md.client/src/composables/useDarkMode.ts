import { ref, onMounted, onUnmounted } from 'vue'

export function useDarkMode() {
  const isDark = ref(false)
  let mediaQuery: MediaQueryList | null = null
  let handler: ((e: MediaQueryListEvent) => void) | null = null

  onMounted(() => {
    mediaQuery = window.matchMedia('(prefers-color-scheme: dark)')
    isDark.value = mediaQuery.matches

    handler = (e: MediaQueryListEvent) => {
      isDark.value = e.matches
      document.documentElement.classList.toggle('dark', e.matches)
    }

    document.documentElement.classList.toggle('dark', isDark.value)
    mediaQuery.addEventListener('change', handler)
  })

  onUnmounted(() => {
    if (mediaQuery && handler) {
      mediaQuery.removeEventListener('change', handler)
    }
  })

  return { isDark }
}
