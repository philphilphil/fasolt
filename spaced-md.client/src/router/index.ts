import { createRouter, createWebHistory } from 'vue-router'
import DashboardView from '@/views/DashboardView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'dashboard', component: DashboardView },
    { path: '/files', name: 'files', component: () => import('@/views/FilesView.vue') },
    { path: '/groups', name: 'groups', component: () => import('@/views/GroupsView.vue') },
    { path: '/review/:deckId?', name: 'review', component: () => import('@/views/ReviewView.vue') },
    { path: '/settings', name: 'settings', component: () => import('@/views/SettingsView.vue') },
  ],
})

export default router
