import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:5266',
        changeOrigin: true,
      },
    },
  },
  build: {
    // dotnet run serves this straight out of wwwroot — see Program.cs
    // UseStaticFiles()/MapFallbackToFile("index.html").
    outDir: '../src/JobApp.Api/wwwroot',
    emptyOutDir: true,
  },
})
