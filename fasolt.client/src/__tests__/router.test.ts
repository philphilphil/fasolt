import { afterEach, describe, expect, it, vi } from 'vitest'

const authState = {
  isLoading: false,
  isAuthenticated: false,
  isEmailConfirmed: false,
  isAdmin: false,
  fetchUser: vi.fn(),
}

vi.mock('@/stores/auth', () => ({
  useAuthStore: () => authState,
}))

describe('router document titles', () => {
  afterEach(() => {
    authState.isLoading = false
    authState.isAuthenticated = false
    authState.isEmailConfirmed = false
    authState.isAdmin = false
    authState.fetchUser.mockReset()
    document.title = ''
    vi.resetModules()
  })

  it('formats the app title consistently', async () => {
    const { formatDocumentTitle } = await import('@/router')
    expect(formatDocumentTitle('Cards')).toBe('Cards - fasolt')
    expect(formatDocumentTitle(undefined)).toBe('fasolt')
  })

  it('updates document.title after navigation', async () => {
    const { default: router } = await import('@/router')
    await router.push('/algorithm')
    await router.isReady()

    expect(document.title).toBe('FSRS algorithm - fasolt')
  })

  it('sets the document title for new legal pages', async () => {
    const { default: router } = await import('@/router')
    await router.push('/terms')
    await router.isReady()
    expect(document.title).toBe('Terms of service - fasolt')

    await router.push('/impressum')
    expect(document.title).toBe('Impressum - fasolt')
  })
})
