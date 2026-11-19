# Multi-stage Dockerfile for MicroLIMS ASP.NET Core 10 Web API
# Build Context: backend/ (or repo root with -f backend/Dockerfile)

# ---- Stage 1: Build & Publish ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for dependency layer caching. The shared build
# settings and central package versions must be present before restore,
# or every project restores with no target framework and no versions.
COPY Directory.Build.props Directory.Packages.props ./
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
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Tesseract OCR for scanned / photographed instrument reports; libldap for Active Directory
# sign-in (System.DirectoryServices.Protocols loads libldap on Linux). The LDAP package is
# libldap2 on Debian 13 and libldap-2.5-0 on Debian 12: install whichever the base image has.
RUN apt-get update \
    && apt-get install -y --no-install-recommends tesseract-ocr tesseract-ocr-eng \
    && (apt-get install -y --no-install-recommends libldap2 || apt-get install -y --no-install-recommends libldap-2.5-0) \
    && rm -rf /var/lib/apt/lists/*

# Ensure storage directory exists with app user ownership before switching user
RUN mkdir -p /app/storage && chown -R app:app /app

# Non-root user setup for production security
USER app

# Default hosting settings (overridden dynamically by Render's PORT variable)
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

COPY --from=build --chown=app:app /app/publish .

ENTRYPOINT ["dotnet", "MicroLIMS.API.dll"]
