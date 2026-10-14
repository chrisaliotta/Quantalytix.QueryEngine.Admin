/// <reference types="vitest/config" />
import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath, URL } from 'node:url';
import { defineConfig } from 'vite';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
      '@registry': fileURLToPath(new URL('./src/registry', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    strictPort: true,
  },
  test: {
    include: ['src/**/*.test.ts'],
  },
});
