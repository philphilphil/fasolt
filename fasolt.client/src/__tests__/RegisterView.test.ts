import { defineComponent, h } from 'vue'
import { describe, expect, it, beforeEach, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import RegisterView from '@/views/RegisterView.vue'

const push = vi.fn()
const register = vi.fn()

vi.mock('vue-router', async () => {
  const actual = await vi.importActual<typeof import('vue-router')>('vue-router')
  return {
    ...actual,
    useRouter: () => ({ push }),
  }
})

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => ({ register }),
}))

vi.mock('@/composables/useFeatures', () => ({
  useFeatures: () => ({
    features: { value: { githubLogin: false } },
  }),
}))

vi.mock('@/components/ui/checkbox', () => ({
  Checkbox: defineComponent({
    name: 'CheckboxStub',
    props: {
      id: { type: String, default: undefined },
      modelValue: { type: Boolean, default: false },
    },
    emits: ['update:modelValue'],
    setup(props, { emit, attrs }) {
      return () =>
        h('input', {
          ...attrs,
          id: props.id,
          type: 'checkbox',
          checked: props.modelValue,
          onChange: (event: Event) => emit('update:modelValue', (event.target as HTMLInputElement).checked),
        })
    },
  }),
}))

describe('RegisterView', () => {
  beforeEach(() => {
    push.mockReset()
    register.mockReset()
  })

  it('requires terms acceptance before submitting', async () => {
    const wrapper = mount(RegisterView, {
      global: {
        stubs: {
          RouterLink: {
            template: '<a><slot /></a>',
          },
        },
      },
    })

    await wrapper.get('#email').setValue('dev@fasolt.local')
    await wrapper.get('#password').setValue('StrongPass123!')
    await wrapper.get('#confirm-password').setValue('StrongPass123!')
    await wrapper.get('form').trigger('submit.prevent')

    expect(register).not.toHaveBeenCalled()
  })

  it('submits once terms are accepted', async () => {
    register.mockResolvedValue(undefined)

    const wrapper = mount(RegisterView, {
      global: {
        stubs: {
          RouterLink: {
            template: '<a><slot /></a>',
          },
        },
      },
    })

    await wrapper.get('#email').setValue('dev@fasolt.local')
    await wrapper.get('#password').setValue('StrongPass123!')
    await wrapper.get('#confirm-password').setValue('StrongPass123!')
    await wrapper.get('#terms').setValue(true)
    await wrapper.get('form').trigger('submit.prevent')

    expect(register).toHaveBeenCalledWith('dev@fasolt.local', 'StrongPass123!')
    expect(push).toHaveBeenCalledWith('/verify-email')
  })
})
