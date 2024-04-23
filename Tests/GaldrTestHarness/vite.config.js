﻿import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";

// https://vitejs.dev/config/
export default defineConfig(async () => ({
    plugins: [vue()],
    clearScreen: false,
    server: {
        port: 42069,
        strictPort: true,
    },
}));
