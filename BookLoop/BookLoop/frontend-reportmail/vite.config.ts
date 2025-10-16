
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'node:path'

export default defineConfig({
  plugins: [vue()],
  root: '.',
  build: {
    outDir: resolve(__dirname, '../wwwroot/reportmail'),
    emptyOutDir: true,
    sourcemap: false,
    rollupOptions: {
      input: resolve(__dirname, 'index.html'),
      output: {
        entryFileNames: `reportmail.js`,
        chunkFileNames: `reportmail-vendor.js`,
        assetFileNames: `reportmail.[ext]`
      }
    }
  },
  server: {
    port: 5173,
    strictPort: true
  }
})
