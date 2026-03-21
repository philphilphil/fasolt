import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import DashboardView from '@/views/DashboardView.vue'

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
    // App routes (require auth)
    { path: '/', name: 'dashboard', component: DashboardView },
    { path: '/files', name: 'files', component: () => import('@/views/FilesView.vue') },
    { path: '/files/:id', name: 'file-detail', component: () => import('@/views/FileDetailView.vue') },
    { path: '/cards', name: 'cards', component: () => import('@/views/CardsView.vue') },
    { path: '/groups', name: 'groups', component: () => import('@/views/GroupsView.vue') },
    { path: '/review/:deckId?', name: 'review', component: () => import('@/views/ReviewView.vue') },
    { path: '/settings', name: 'settings', component: () => import('@/views/SettingsView.vue') },
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
    return { name: 'dashboard' }
  }
})

export default router
