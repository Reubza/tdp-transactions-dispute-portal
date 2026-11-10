# syntax=docker/dockerfile:1

# ---- 1. Build the Angular SPA -------------------------------------------------
FROM node:24-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY web/ ./
RUN npm run build -- --configuration production

# ---- 2. Build and publish the ASP.NET Core API (with the SPA in wwwroot) -------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props .editorconfig ./
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Application/Application.csproj src/Application/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/Api/Api.csproj src/Api/
RUN dotnet restore src/Api/Api.csproj
COPY src/ src/
COPY --from=web /web/dist/web/browser/ src/Api/wwwroot/
RUN dotnet publish src/Api/Api.csproj \
      --configuration Release --no-restore --output /app /p:UseAppHost=false

# ---- 3. Runtime image ----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true
# Npgsql probes for GSSAPI at connect time; without it every connection logs a load error.
RUN apk add --no-cache krb5-libs
COPY --from=build /app ./
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=3s --start-period=20s --retries=5 \
  CMD wget -q --spider http://localhost:8080/health/live || exit 1
ENTRYPOINT ["dotnet", "Api.dll"]
