#!/bin/bash
# Placeholder deployment script - adapt to your target
# (IIS, Docker, Azure App Service, etc.)
set -e
echo "1. Build backend"
(cd ../../backend/MicroLIMS.API && dotnet publish -c Release -o ./publish)
echo "2. Build frontend"
(cd ../../frontend && npm ci && npm run build)
echo "3. Copy artifacts to your deployment target here."
