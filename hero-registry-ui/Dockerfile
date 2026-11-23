# Angular UI (T15): build with Node, serve the static files with nginx.
# Build context: this folder (hero-registry-ui).
#   docker build -t hero-registry-ui .

# 1. Build. Angular 22 needs Node ^22.22.3; the 22-alpine tag tracks the latest 22.x.
FROM node:22-alpine AS build
WORKDIR /app
ENV NG_CLI_ANALYTICS=false
# Dependencies first, so this layer stays cached until package*.json change.
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npm run build

# 2. Serve. The same nginx proxies /api to the API container, so the browser sees one origin:
#    no CORS, and the HttpOnly refresh cookie (Path=/api/auth) just works.
FROM nginx:alpine AS final
COPY nginx/default.conf /etc/nginx/conf.d/default.conf
COPY nginx/security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=build /app/dist/hero-registry-ui/browser /usr/share/nginx/html
EXPOSE 80
