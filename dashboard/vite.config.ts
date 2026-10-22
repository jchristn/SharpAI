import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    strictPort: false,
    proxy: { '/engine-metrics': { target: 'http://localhost:8889', changeOrigin: true, rewrite: () => '/metrics' }, '^/(api(/|$)|v1(\\.0)?(/|$)|health$|ready$|metrics$|openapi\\.json$)': { target: 'http://localhost:8000', changeOrigin: true } },
  },
  preview: {
    port: 3000,
  },
  build: {
    outDir: "dist",
    sourcemap: true,
  },
  resolve: {
    tsconfigPaths: true,
    alias: {
      "#": "/src",
    },
  },
});
