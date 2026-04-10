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
      '/login': 'http://localhost:8080',
      '/register': 'http://localhost:8080',
      '/oauth': 'http://localhost:8080',
    },
  },
})
