import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    strictPort: false,
    proxy: { '/engine-metrics': {
      target: 'http://localhost:8889', changeOrigin: true, rewrite: () => '/metrics',
      async bypass(request, response) {
        let status = 401;
        try {
          const authorization = request.headers.authorization;
          if (authorization?.startsWith('Bearer ')) {
            const result = await fetch('http://localhost:8000/v1.0/auth/context', {headers:{Authorization:authorization},signal:AbortSignal.timeout(3000)});
            if (result.ok) return;
            status = result.status === 401 ? 401 : 503;
          }
        } catch { status = 503; }
        if (response) {
          response.statusCode = status;
          response.setHeader('Content-Type','application/json');
          if (status === 401) response.setHeader('WWW-Authenticate','Bearer');
          response.end(JSON.stringify({error:status === 401 ? 'Bearer authentication is required.' : 'Authentication service is unavailable.'}));
        }
        return '/engine-metrics';
      }
    }, '^/(api(/|$)|v1(\\.0)?(/|$)|health$|ready$|metrics$|openapi\\.json$)': { target: 'http://localhost:8000', changeOrigin: true } },
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
