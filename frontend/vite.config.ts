import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import { readFileSync } from 'node:fs'

const pkg = JSON.parse(readFileSync(new URL('./package.json', import.meta.url), 'utf-8')) as { version: string }

export default defineConfig({
  plugins: [react()],
  define: {
    __APP_VERSION__: JSON.stringify(pkg.version)
  },
  test: {
    environment: 'jsdom',
    globals: false,
    testTimeout: 20000,
    hookTimeout: 20000,
    setupFiles: ['./src/setupTests.ts'],
    maxWorkers: 4
  },
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
