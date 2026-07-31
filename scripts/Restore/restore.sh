#!/bin/bash
# Usage: ./restore.sh path/to/backup.dump
set -e
if [ -z "$1" ]; then
  echo "Usage: ./restore.sh <backup-file>"
  exit 1
fi
pg_restore -h localhost -U microlims_user -d microlims --clean --if-exists "$1"
echo "Restore complete."
