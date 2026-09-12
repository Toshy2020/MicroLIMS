import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  // MUI 5 publishes @mui/icons-material/<Icon> as CommonJS with
  // exports.default, and the codebase default-imports ~640 of them by path.
  // Vite 8's consistent CJS interop hands those imports the whole exports
  // object, so every icon renders as an invalid element (React error #130).
  // Restores the pre-Vite-8 interop until MUI 9, whose icons ship an
  // exports map with ESM entries - remove this flag with that upgrade.
  legacy: {
    inconsistentCjsInterop: true
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
