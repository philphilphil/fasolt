import path from 'node:path'
import autoprefixer from 'autoprefixer'
import tailwind from 'tailwindcss'
import { defineConfig } from 'vite'

// Dedicated config for the shared /oauth/* Razor Pages stylesheet. Writes
// directly to fasolt.Server/wwwroot/css/auth.css (bypassing fasolt.client/dist)
// so the dev flow and production build can both hit a stable path. The
// Razor _Layout.cshtml links via <link href="~/css/auth.css" asp-append-version>
// which hashes at serve time for cache busting — no hashed filename needed.
export default defineConfig({
  css: {
    postcss: {
      plugins: [tailwind(), autoprefixer()],
    },
  },
  build: {
    // Absolute output dir — outside fasolt.client to reach the server wwwroot.
    outDir: path.resolve(__dirname, '../fasolt.Server/wwwroot'),
    // We're writing into the server's wwwroot — never wipe it.
    emptyOutDir: false,
    // CSS-only entry — Rollup emits no JS chunk for .css inputs in Vite 8+.
    // If a stray auth.js appears in wwwroot/css/ after a Vite upgrade, either
    // switch to build.lib mode or use a standalone PostCSS build pipeline.
    rollupOptions: {
      input: {
        auth: path.resolve(__dirname, 'src/auth.css'),
      },
      output: {
        // Unhashed name so the Razor <link href="~/css/auth.css"> stays stable.
        assetFileNames: (assetInfo) => {
          if (assetInfo.name === 'auth.css') return 'css/auth.css'
          return 'css/[name][extname]'
        },
      },
    },
  },
})
