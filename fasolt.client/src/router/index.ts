import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import StudyView from '@/views/StudyView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    // Auth routes (public)
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/LoginView.vue'),
      meta: { public: true, authRedirect: true },
    },
    {
      path: '/register',
      name: 'register',
      component: () => import('@/views/RegisterView.vue'),
      meta: { public: true, authRedirect: true },
    },
    {
      path: '/forgot-password',
      name: 'forgot-password',
      component: () => import('@/views/ForgotPasswordView.vue'),
      meta: { public: true },
    },
    {
      path: '/reset-password',
      name: 'reset-password',
      component: () => import('@/views/ResetPasswordView.vue'),
      meta: { public: true },
    },
    {
      path: '/oauth/consent',
      name: 'oauth-consent',
      component: () => import('@/views/OAuthConsentView.vue'),
      meta: { public: true },
    },
    // Landing page (public)
    {
      path: '/',
      name: 'landing',
      component: () => import('@/views/LandingView.vue'),
      meta: { public: true, authRedirect: true, landing: true },
    },
    {
      path: '/algorithm',
      name: 'algorithm',
      component: () => import('@/views/AlgorithmView.vue'),
      meta: { public: true },
    },
    // App routes (require auth)
    { path: '/study', name: 'study', component: StudyView },
    { path: '/sources', name: 'sources', component: () => import('@/views/SourcesView.vue') },
    { path: '/cards', name: 'cards', component: () => import('@/views/CardsView.vue') },
    { path: '/cards/:id', name: 'card-detail', component: () => import('@/views/CardDetailView.vue') },
    { path: '/decks', name: 'decks', component: () => import('@/views/DecksView.vue') },
    { path: '/decks/:id', name: 'deck-detail', component: () => import('@/views/DeckDetailView.vue') },
    { path: '/review/:deckId?', name: 'review', component: () => import('@/views/ReviewView.vue') },
    { path: '/mcp', name: 'mcp', component: () => import('@/views/McpView.vue') },
    { path: '/settings', name: 'settings', component: () => import('@/views/SettingsView.vue') },
    { path: '/admin', name: 'admin', component: () => import('@/views/AdminView.vue'), meta: { requiresAdmin: true } },
    { path: '/dashboard', redirect: '/study' },
    // Catch-all 404
    { path: '/:pathMatch(.*)*', name: 'not-found', component: () => import('@/views/NotFoundView.vue'), meta: { public: true } },
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

export default router
