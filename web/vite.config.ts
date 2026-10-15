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
    // The API's `http` launch profile. Calls reach it through the dev server's own origin, so a browser on another
    // machine or outside WSL needs neither a CORS entry for its origin nor a route to the API's port.
    proxy: {
      '/api': 'http://localhost:5180',
      '/development': 'http://localhost:5180',
    },
  },
  test: {
    include: ['src/**/*.test.ts'],
    // A zone away from UTC, so a time shown or edited in local time instead of UTC fails on every machine.
    env: { TZ: 'Asia/Tokyo' },
  },
});
