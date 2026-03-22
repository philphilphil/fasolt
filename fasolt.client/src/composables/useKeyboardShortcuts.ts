type ShortcutMap = Record<string, () => void>

export function useKeyboardShortcuts() {
  let listener: ((e: KeyboardEvent) => void) | null = null

  function register(shortcuts: ShortcutMap) {
    cleanup()
    listener = (e: KeyboardEvent) => {
      // Don't fire when typing in inputs
      const tag = (e.target as HTMLElement)?.tagName
      if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return

      const parts: string[] = []
      if (e.metaKey || e.ctrlKey) parts.push('meta')
      parts.push(e.key.toLowerCase())
      const combo = parts.join('+')

      const handler = shortcuts[combo] ?? shortcuts[e.key]
      if (handler) {
        e.preventDefault()
        handler()
      }
    }
    document.addEventListener('keydown', listener)
  }

  function cleanup() {
    if (listener) {
      document.removeEventListener('keydown', listener)
      listener = null
    }
  }

  return { register, cleanup }
}
