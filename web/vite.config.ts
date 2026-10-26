import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // The API serves everything under /api; proxying keeps the browser on one origin.
    proxy: { '/api': 'http://localhost:8080' },
  },
  test: { environment: 'node' },
})
