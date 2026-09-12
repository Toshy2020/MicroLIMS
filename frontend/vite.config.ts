import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: process.env.PORT ? Number(process.env.PORT) : 5173
  },
  build: {
    chunkSizeWarningLimit: 650,
    // Vite 8 bundles with Rolldown, which rejects the object form of
    // manualChunks. The same three vendor chunks are declared as
    // codeSplitting groups; [\\/] matches both Windows and POSIX paths.
    rolldownOptions: {
      output: {
        codeSplitting: {
          groups: [
            {
              name: 'vendor-react',
              test: /[\\/]node_modules[\\/](react|react-dom|react-router|react-router-dom|@remix-run[\\/]router|scheduler|axios|sonner)[\\/]/
            },
            {
              name: 'vendor-mui',
              test: /[\\/]node_modules[\\/](@mui|@emotion)[\\/]/
            },
            {
              name: 'vendor-charts',
              test: /[\\/]node_modules[\\/]recharts[\\/]/
            }
          ]
        }
      }
    }
  }
})
