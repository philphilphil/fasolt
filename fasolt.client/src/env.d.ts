import 'vue-router'

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
