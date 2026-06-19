import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// base: './' so built assets resolve under file:// (FolderContent) in release.
// Port is pinned to match UrlContent("http://localhost:5174") in Program.cs.
export default defineConfig({
  plugins: [react()],
  base: './',
  server: {
    port: 5174,
    strictPort: true,
  },
})
