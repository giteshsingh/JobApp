import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// Builds straight into the ASP.NET Core project's wwwroot, so `dotnet run`
// serves the compiled dashboard with no separate frontend host in production.
// `npm run dev` still uses the Vite dev server with the proxy below.
export default defineConfig({
  plugins: [vue()],
  build: {
    outDir: '../src/JobApp.Api/wwwroot',
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:5266',
        changeOrigin: true,
      },
    },
  },
})
