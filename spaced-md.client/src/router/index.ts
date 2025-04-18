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
          path: '/mdfiles/list',
          name: 'mdfiles-list',
          component: () => import('@/views/mdfiles/MdFiles.vue')
        },
        {
          path: '/documentation',
          name: 'documentation',
          component: () => import('@/views/pages/Documentation.vue')
        }
      ]
    },
    {
      path: '/auth/login',
      name: 'login',
      component: () => import('@/views/pages/auth/Login.vue')
    },
    {
      path: '/auth/access',
      name: 'accessDenied',
      component: () => import('@/views/pages/auth/Access.vue')
    },
    {
      path: '/auth/error',
      name: 'error',
      component: () => import('@/views/pages/auth/Error.vue')
    }
  ]
});


export default router;
