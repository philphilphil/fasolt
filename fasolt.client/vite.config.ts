import path from 'node:path'
import vue from '@vitejs/plugin-vue'
import autoprefixer from 'autoprefixer'
import tailwind from 'tailwindcss'
import { defineConfig } from 'vite'

export default defineConfig({
  css: {
    postcss: {
      plugins: [tailwind(), autoprefixer()],
    },
  },
  plugins: [vue()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:8080',
        headers: { 'X-Forwarded-Host': 'localhost:5173', 'X-Forwarded-Proto': 'http' },
      },
      '/signin-github': {
        target: 'http://localhost:8080',
        headers: { 'X-Forwarded-Host': 'localhost:5173', 'X-Forwarded-Proto': 'http' },
      },
      '/mcp': {
        target: 'http://localhost:8080',
        rewrite: undefined,
        bypass(req) {
          // Only proxy exact /mcp path, not /mcp-setup (which is a frontend route)
          if (req.url?.startsWith('/mcp-setup')) return req.url
        },
      },
      '/.well-known': 'http://localhost:8080',
      '/css': 'http://localhost:8080',
      '/js': 'http://localhost:8080',
      '/oauth/register': 'http://localhost:8080',
      '/oauth/verify-email': 'http://localhost:8080',
      '/oauth/forgot-password': 'http://localhost:8080',
      '/oauth/reset-password': 'http://localhost:8080',
      '/oauth/authorize': 'http://localhost:8080',
      '/oauth/token': 'http://localhost:8080',
      '/oauth/login': 'http://localhost:8080',
      '/oauth/consent': 'http://localhost:8080',
      '/oauth/clients/register': 'http://localhost:8080',
      // Legacy auth paths — the server 301-redirects these to /oauth/*
      // so stale bookmarks still work. In dev, Vite needs to forward
      // them to the backend so the redirect actually fires (otherwise
      // Vite serves index.html and the SPA shows a 404).
      '/register': 'http://localhost:8080',
      '/verify-email': 'http://localhost:8080',
      '/confirm-email': 'http://localhost:8080',
      '/forgot-password': 'http://localhost:8080',
      '/reset-password': 'http://localhost:8080',
    },
  },
})
