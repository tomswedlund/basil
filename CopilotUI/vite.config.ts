import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  base: '/app/',
  server: {
    host: '0.0.0.0',
    allowedHosts: ['deepseek-harness', 'localhost', '192.168.1.11', '172.23.0.3'],
    proxy: {
      '/api/v1': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      }
    }
  }
})
