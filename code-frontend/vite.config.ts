import { defineConfig } from 'vite'
import tailwindcss from '@tailwindcss/vite'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue(), tailwindcss()],
  server: {
    proxy: {
      '/api': {
        target: process.env.RONFLOW_API_PROXY_TARGET ?? 'http://127.0.0.1:5078',
        changeOrigin: true,
      },
    },
  },
})
