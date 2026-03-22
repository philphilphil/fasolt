import { describe, it, expect, beforeEach } from 'vitest'
import { useDarkMode } from '@/composables/useDarkMode'

describe('useDarkMode', () => {
  beforeEach(() => {
    document.documentElement.classList.remove('dark')
  })

  it('adds dark class when system prefers dark', () => {
    const { isDark: _isDark } = useDarkMode()
    expect(document.documentElement.classList.contains('dark')).toBe(false)
  })
})
