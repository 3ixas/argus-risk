# Multi-stage Dockerfile for Argus Web (Next.js)
# Usage: docker build -f docker/web.Dockerfile .

# ── Stage 1: Dependencies ──
FROM node:20-alpine AS deps
WORKDIR /app
COPY src/Argus.Web/package.json src/Argus.Web/package-lock.json ./
RUN npm ci

# ── Stage 2: Build ──
FROM node:20-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY src/Argus.Web/ .

# NEXT_PUBLIC_* vars are baked into the JS bundle at build time
# Browser connects to the API via the host machine's localhost
ARG NEXT_PUBLIC_API_URL=http://localhost:5000
ARG NEXT_PUBLIC_SIGNALR_URL=http://localhost:5000/hubs/risk
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL
ENV NEXT_PUBLIC_SIGNALR_URL=$NEXT_PUBLIC_SIGNALR_URL

RUN npm run build

# ── Stage 3: Runtime ──
FROM node:20-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production

# Copy standalone server + static assets
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static

EXPOSE 3000
ENV HOSTNAME="0.0.0.0"
CMD ["node", "server.js"]
