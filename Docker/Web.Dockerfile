FROM node:24-alpine AS build
WORKDIR /src
COPY Coiny.Web/package*.json ./
RUN npm ci
COPY Coiny.Web/ ./
RUN npx ng build --configuration container --output-path /out
FROM caddy:2-alpine
COPY --from=build /out/browser /srv
COPY Docker/Caddyfile /etc/caddy/Caddyfile
