import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'
import Components from 'unplugin-vue-components/vite'
import Icons from 'unplugin-icons/vite'
import IconsResolver from 'unplugin-icons/resolver'

export default defineConfig({
  plugins: [
    vue(),
    tailwindcss(),
    Components({
      resolvers: [IconsResolver()],
      dts: false,
    }),
    Icons({ compiler: 'vue3' }),
  ],
  base: './',
  server: {
    port: 5174,
    strictPort: true,
  },
})
