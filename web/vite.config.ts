import path from 'node:path'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

const rootDir = import.meta.dirname

// Backend dev port from src/Tendaa.Web/Properties/launchSettings.json — present in both the
// "http" and "https" profiles, so it's bound regardless of which one `dotnet run` picks.
const backendDevUrl = 'http://localhost:5093'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: {
    outDir: path.resolve(rootDir, '../src/Tendaa.Web/wwwroot'),
    emptyOutDir: true,
  },
  server: {
    proxy: {
      '/api': { target: backendDevUrl, changeOrigin: true },
      '/webhooks': { target: backendDevUrl, changeOrigin: true },
      '/hubs': { target: backendDevUrl, changeOrigin: true, ws: true },
    },
  },
})
