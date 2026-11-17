# The web image: the Angular page built once, served by nginx, which forwards /api to the API container.
FROM node:24-alpine AS build
WORKDIR /app

# The package files first, so the packages are installed again only when they change.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:stable-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/scriptorium.web/browser /usr/share/nginx/html
EXPOSE 80
