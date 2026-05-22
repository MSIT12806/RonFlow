import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import tailwindcss from '@tailwindcss/vite'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue(), tailwindcss()],
  resolve: {
    alias: {
      '@ronauth/sdk': fileURLToPath(new URL('../../RonAuth/sdk/src/index.ts', import.meta.url)),
    },
  },
  server: {
    proxy: {
      '/api': {
        target: process.env.RONFLOW_API_PROXY_TARGET ?? 'http://127.0.0.1:5078',
        changeOrigin: true,
      },
      '/ronauth-api': {
        target: process.env.RONAUTH_API_PROXY_TARGET ?? 'http://127.0.0.1:5136',
        changeOrigin: true,
      },
    },
  },
})
