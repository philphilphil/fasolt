import AppLayout from '@/layout/AppLayout.vue';
import { createRouter, createWebHistory } from 'vue-router';

const router = createRouter({

  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'Landing',
      component: () => import('@/views/pages/Landing.vue'),
      meta: { layout: 'landing' }
    },
    {
      path: '/',
      component: AppLayout,
      meta: { requiresAuth: true },
      children: [
        {
          path: '/home',
          name: 'home',
          component: () => import('@/views/Dashboard.vue')
        },
        {
          path: '/mdfiles',
          name: 'mdfiles',
          component: () => import('@/views/MdFiles.vue')
        },
        {
          path: '/cards',
          name: 'cards',
          component: () => import('@/views/Cards.vue')
        },
        {
          path: '/groups',
          name: 'groups',
          component: () => import('@/views/Groups.vue')
        },
        {
          path: '/study',
          name: 'study',
          component: () => import('@/views/Study.vue')
        },
        {
          path: '/documentation',
          name: 'documentation',
          component: () => import('@/views/pages/Documentation.vue')
        },
        {
          path: '/profile',
          name: 'profile',
          component: () => import('@/views/user/UserProfile.vue')
        }
      ]
    },
    {
      path: '/auth/login',
      name: 'login',
      component: () => import('@/views/auth/Login.vue')
    },
    {
      path: '/auth/access',
      name: 'accessDenied',
      component: () => import('@/views/auth/Access.vue')
    },
    {
      path: '/auth/error',
      name: 'error',
      component: () => import('@/views/auth/Error.vue')
    }
  ]
});


export default router;
