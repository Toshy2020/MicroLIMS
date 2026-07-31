#!/bin/bash
# Applies pending EF Core migrations to the target database.
set -e
cd ../../backend/MicroLIMS.API
dotnet ef database update --project ../MicroLIMS.Persistence --startup-project .
