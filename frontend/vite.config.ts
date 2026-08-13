import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

const apiTarget = process.env.VITE_API_TARGET ?? 'http://localhost:5080'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    host: true,
    proxy: {
      '/api': {
        target: apiTarget,
        changeOrigin: true,
        ws: true,
      },
      '/health': {
        target: apiTarget,
        changeOrigin: true,
      },
      '/hubs': {
        target: apiTarget,
        changeOrigin: true,
        ws: true,
      },
    },
  },
})