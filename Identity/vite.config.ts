import { fileURLToPath, URL } from 'node:url';
import vue from '@vitejs/plugin-vue';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [vue()],
  build: {
    lib: { entry: fileURLToPath(new URL('./src/index.ts', import.meta.url)), formats: ['es'], fileName: () => 'pebble-identity.js', cssFileName: 'styles' },
    rollupOptions: { external: ['vue', 'vue-i18n', '@pebble/ui'] },
  },
  test: { environment: 'jsdom' },
});
