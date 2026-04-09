import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import StudyView from '@/views/StudyView.vue'

const appName = 'fasolt'

function formatDocumentTitle(title?: string) {
  return title ? `${title} - ${appName}` : appName
}

const router = createRouter({
  history: createWebHistory(),
  routes: [
    // Auth routes (public)
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/LoginView.vue'),
      meta: { public: true, authRedirect: true, title: 'Log in' },
    },
    // /forgot-password and /reset-password are handled by the backend: it
    // 301s to the server-rendered /oauth/forgot-password + /oauth/reset-password
    // OTP pages. No SPA routes needed — the backend redirect runs before the
    // Vue router ever sees the path.
    {
      path: '/confirm-email-change',
      name: 'confirm-email-change',
      component: () => import('@/views/ConfirmEmailChangeView.vue'),
      meta: { skipVerificationCheck: true, title: 'Confirm email change' },
    },
    {
      path: '/oauth/consent',
      name: 'oauth-consent',
      component: () => import('@/views/OAuthConsentView.vue'),
      meta: { public: true, title: 'Authorize app' },
    },
    // Landing page (public)
    {
      path: '/',
      name: 'landing',
      component: () => import('@/views/LandingView.vue'),
      meta: { public: true, authRedirect: true, landing: true, title: 'MCP-first spaced repetition for markdown notes' },
    },
    {
      path: '/algorithm',
      name: 'algorithm',
      component: () => import('@/views/AlgorithmView.vue'),
      meta: { public: true, landing: true, title: 'FSRS algorithm' },
    },
    {
      path: '/privacy',
      name: 'privacy',
      component: () => import('@/views/PrivacyPolicyView.vue'),
      meta: { public: true, landing: true, title: 'Privacy policy' },
    },
    {
      path: '/impressum',
      name: 'impressum',
      component: () => import('@/views/ImpressumView.vue'),
      meta: { public: true, landing: true, title: 'Impressum' },
    },
    {
      path: '/terms',
      name: 'terms',
      component: () => import('@/views/TermsOfServiceView.vue'),
      meta: { public: true, landing: true, title: 'Terms of service' },
    },
    // App routes (require auth)
    { path: '/study', name: 'study', component: StudyView, meta: { title: 'Study' } },
    { path: '/sources', name: 'sources', component: () => import('@/views/SourcesView.vue'), meta: { title: 'Sources' } },
    { path: '/cards', name: 'cards', component: () => import('@/views/CardsView.vue'), meta: { title: 'Cards' } },
    { path: '/cards/:id', name: 'card-detail', component: () => import('@/views/CardDetailView.vue'), meta: { title: 'Card details' } },
    { path: '/decks', name: 'decks', component: () => import('@/views/DecksView.vue'), meta: { title: 'Decks' } },
    { path: '/decks/:id', name: 'deck-detail', component: () => import('@/views/DeckDetailView.vue'), meta: { title: 'Deck details' } },
    { path: '/decks/:id/snapshots', name: 'deck-snapshots', component: () => import('@/views/DeckSnapshotsView.vue'), meta: { title: 'Deck snapshots' } },
    { path: '/decks/:id/snapshots/:snapshotId/restore', name: 'restore-snapshot', component: () => import('@/views/RestoreSnapshotView.vue'), meta: { title: 'Restore snapshot' } },
    { path: '/review/:deckId?', name: 'review', component: () => import('@/views/ReviewView.vue'), meta: { title: 'Review' } },
    { path: '/mcp-setup', name: 'mcp', component: () => import('@/views/McpView.vue'), meta: { title: 'MCP setup' } },
    { path: '/settings', name: 'settings', component: () => import('@/views/SettingsView.vue'), meta: { title: 'Settings' } },
    { path: '/admin', name: 'admin', component: () => import('@/views/AdminView.vue'), meta: { requiresAdmin: true, title: 'Admin' } },
    { path: '/dashboard', redirect: '/study' },
    // Catch-all 404
    { path: '/:pathMatch(.*)*', name: 'not-found', component: () => import('@/views/NotFoundView.vue'), meta: { public: true, title: 'Page not found' } },
  ],
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()

  // Wait for initial auth check
  if (auth.isLoading) {
    await auth.fetchUser()
  }

  const isPublic = to.meta.public === true

  if (!isPublic && !auth.isAuthenticated) {
    return { name: 'login' }
  }

  if (to.meta.authRedirect && auth.isAuthenticated) {
    return { name: 'study' }
  }

  if (to.meta.requiresAdmin) {
    if (!auth.isAdmin) {
      return { name: 'study' }
    }
  }
})

router.afterEach((to) => {
  document.title = formatDocumentTitle(to.meta.title)
})

export default router
export { formatDocumentTitle }
