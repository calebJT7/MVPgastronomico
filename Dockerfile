# ==========================================================
# Multi-stage Dockerfile for Gastronomico SaaS (.NET 9)
# Combines Blazor WebAssembly Frontend + ASP.NET Core Web API
# ==========================================================

# --- Stage 1: Build Blazor WASM Frontend ---
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-web
WORKDIR /src
COPY ["GastronomicoWeb/RotiseriaWeb.csproj", "GastronomicoWeb/"]
RUN dotnet restore "GastronomicoWeb/RotiseriaWeb.csproj"
COPY GastronomicoWeb/ GastronomicoWeb/
WORKDIR /src/GastronomicoWeb
RUN dotnet publish "RotiseriaWeb.csproj" -c Release -o /app/web_publish

# --- Stage 2: Build ASP.NET Core Backend API ---
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-api
WORKDIR /src
COPY ["GastronomicoAPI/RotiseriaAPI.csproj", "GastronomicoAPI/"]
RUN dotnet restore "GastronomicoAPI/RotiseriaAPI.csproj"
COPY GastronomicoAPI/ GastronomicoAPI/
WORKDIR /src/GastronomicoAPI
RUN dotnet publish "RotiseriaAPI.csproj" -c Release -o /app/api_publish

# Copy Blazor WASM static files into API's wwwroot for single-origin serving
COPY --from=build-web /app/web_publish/wwwroot /app/api_publish/wwwroot

# --- Stage 3: Production Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for container healthchecks
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build-api /app/api_publish .

# Expose standard ASP.NET container port
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "RotiseriaAPI.dll"]
