#
# Dashboard Dockerfile
# Multi-stage: build the Vite SPA, then serve dist/ with nginx.
#

# -------- Build stage --------
FROM node:22-slim AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

# -------- Runtime stage --------
FROM nginx:alpine
# Serve the Vite-built static SPA on port 3000. The SPA is purely client-side;
# it calls the SharpAI server at whatever URL the user configures on the
# landing page (default http://localhost:8000, overridable at runtime).
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 3000
CMD ["nginx", "-g", "daemon off;"]
