#!/bin/bash
# Simple pg_dump backup script. Schedule via cron or a CI pipeline.
# Usage: ./backup.sh
set -e
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
OUT_DIR="../../database/Backup"
mkdir -p "$OUT_DIR"
pg_dump -h localhost -U microlims_user -d microlims -F c -f "$OUT_DIR/microlims_$TIMESTAMP.dump"
echo "Backup written to $OUT_DIR/microlims_$TIMESTAMP.dump"
