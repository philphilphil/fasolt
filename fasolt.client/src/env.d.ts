import 'vue-router'

declare global {
  const __APP_VERSION__: string
}

declare module 'vue-router' {
  interface RouteMeta {
    public?: boolean
    authRedirect?: boolean
    landing?: boolean
    skipVerificationCheck?: boolean
    requiresAdmin?: boolean
    title?: string
  }
}

export {}
