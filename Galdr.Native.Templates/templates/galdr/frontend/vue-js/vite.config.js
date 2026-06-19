import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// base: './' so built assets resolve under file:// (FolderContent) in release.
// Port is pinned to match UrlContent("http://localhost:5174") in Program.cs.
export default defineConfig({
  plugins: [vue()],
  base: './',
  server: {
    port: 5174,
    strictPort: true,
  },
})
