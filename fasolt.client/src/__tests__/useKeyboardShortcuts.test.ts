import { describe, it, expect, vi, afterEach } from 'vitest'
import { useKeyboardShortcuts } from '@/composables/useKeyboardShortcuts'
import { mount } from '@vue/test-utils'
import { defineComponent, onMounted, onUnmounted } from 'vue'

function mountWithShortcuts(shortcuts: Record<string, () => void>) {
  return mount(defineComponent({
    setup() {
      const { register, cleanup } = useKeyboardShortcuts()
      onMounted(() => register(shortcuts))
      onUnmounted(() => cleanup())
      return {}
    },
    template: '<div />',
  }))
}

describe('useKeyboardShortcuts', () => {
  afterEach(() => {
    document.body.innerHTML = ''
  })

  it('calls handler when matching key is pressed', () => {
    const handler = vi.fn()
    const wrapper = mountWithShortcuts({ ' ': handler })
    document.dispatchEvent(new KeyboardEvent('keydown', { key: ' ' }))
    expect(handler).toHaveBeenCalledOnce()
    wrapper.unmount()
  })

  it('does not call handler after cleanup', () => {
    const handler = vi.fn()
    const wrapper = mountWithShortcuts({ ' ': handler })
    wrapper.unmount()
    document.dispatchEvent(new KeyboardEvent('keydown', { key: ' ' }))
    expect(handler).not.toHaveBeenCalled()
  })

  it('handles meta+key combinations', () => {
    const handler = vi.fn()
    const wrapper = mountWithShortcuts({ 'meta+k': handler })
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', metaKey: true }))
    expect(handler).toHaveBeenCalledOnce()
    wrapper.unmount()
  })
})
