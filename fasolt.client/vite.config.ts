import path from 'node:path'
import vue from '@vitejs/plugin-vue'
import autoprefixer from 'autoprefixer'
import tailwind from 'tailwindcss'
import { defineConfig } from 'vite'
import pkg from './package.json' with { type: 'json' }

export default defineConfig({
  define: {
    __APP_VERSION__: JSON.stringify(pkg.version),
  },
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
      '/oauth/register': 'http://localhost:8080',
      '/oauth/authorize': 'http://localhost:8080',
      '/oauth/token': 'http://localhost:8080',
      '/oauth/login': 'http://localhost:8080',
    },
  },
})
