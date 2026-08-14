# Multi-stage Dockerfile for MicroLIMS ASP.NET Core 8 Web API
# Build Context: backend/ (or repo root with -f backend/Dockerfile)

# ---- Stage 1: Build & Publish ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for dependency layer caching
COPY MicroLIMS.Shared/MicroLIMS.Shared.csproj MicroLIMS.Shared/
COPY MicroLIMS.Domain/MicroLIMS.Domain.csproj MicroLIMS.Domain/
COPY MicroLIMS.Application/MicroLIMS.Application.csproj MicroLIMS.Application/
COPY MicroLIMS.Persistence/MicroLIMS.Persistence.csproj MicroLIMS.Persistence/
COPY MicroLIMS.Infrastructure/MicroLIMS.Infrastructure.csproj MicroLIMS.Infrastructure/
COPY MicroLIMS.API/MicroLIMS.API.csproj MicroLIMS.API/

# Restore dependencies
RUN dotnet restore MicroLIMS.API/MicroLIMS.API.csproj

# Copy all source code
COPY . .

# Build and publish release binaries
WORKDIR /src/MicroLIMS.API
RUN dotnet publish MicroLIMS.API.csproj -c Release -o /app/publish --no-restore

# ---- Stage 2: Runtime Image ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Non-root user setup for production security
USER app

# Default hosting settings (overridden dynamically by Render's PORT variable)
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "MicroLIMS.API.dll"]
