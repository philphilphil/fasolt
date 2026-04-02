# --- Build frontend ---
FROM node:22-alpine AS frontend
WORKDIR /app
COPY fasolt.client/package.json fasolt.client/package-lock.json ./
RUN npm ci
COPY fasolt.client/ ./
ARG VITE_BUGSINK_DSN=""
ENV VITE_BUGSINK_DSN=$VITE_BUGSINK_DSN
RUN npm run build

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
RUN addgroup --system fasolt && adduser --system --ingroup fasolt fasolt
WORKDIR /app
COPY --from=backend /app/publish ./
COPY --from=frontend /app/dist ./wwwroot/
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER fasolt
ENTRYPOINT ["dotnet", "fasolt.Server.dll"]
