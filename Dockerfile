# --- Build frontend ---
FROM node:22-alpine AS frontend
WORKDIR /app
COPY fasolt.client/package.json fasolt.client/package-lock.json ./
RUN npm ci
COPY fasolt.client/ ./
# Copy the Razor pages directory so the Tailwind content scanner can
# find utility classes used in .cshtml templates when building auth.css.
# The destination "../fasolt.Server/Pages/" resolves to /fasolt.Server/Pages/
# because WORKDIR is /app — this MUST match the 'content' glob in
# tailwind.config.js ('../fasolt.Server/Pages/**/*.cshtml' relative to
# fasolt.client/, which is also /app inside this stage). If WORKDIR ever
# changes, update both ends of this path coupling together.
COPY fasolt.Server/Pages/ ../fasolt.Server/Pages/
ARG VITE_AXIOM_TOKEN=""
ARG VITE_AXIOM_DATASET=""
ENV VITE_AXIOM_TOKEN=$VITE_AXIOM_TOKEN
ENV VITE_AXIOM_DATASET=$VITE_AXIOM_DATASET
RUN npm run build
RUN npm run build:auth

# --- Build backend ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY fasolt.Server/fasolt.Server.csproj fasolt.Server/
RUN dotnet restore fasolt.Server/fasolt.Server.csproj
COPY fasolt.Server/ fasolt.Server/
RUN dotnet publish fasolt.Server/fasolt.Server.csproj -c Release -o /app/publish --no-restore

# --- Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends libkrb5-3 curl && rm -rf /var/lib/apt/lists/*
RUN groupadd --system fasolt && useradd --system --gid fasolt --no-create-home fasolt
WORKDIR /app
COPY --from=backend /app/publish ./
COPY --from=frontend /app/dist ./wwwroot/
COPY --from=frontend /fasolt.Server/wwwroot/css/auth.css ./wwwroot/css/auth.css
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER fasolt
ENTRYPOINT ["dotnet", "fasolt.Server.dll"]
