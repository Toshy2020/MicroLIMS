#!/bin/bash
# Simple pg_dump backup script. Schedule via cron or a CI pipeline.
# Usage: ./backup.sh
#
# After every attempt, success or failure, one row is written to BackupRuns
# so the admin Overview can show the last backup. Recording is best-effort:
# if the row cannot be written, a warning is printed and the backup result
# (and exit status) is unchanged. No credentials are read or printed here;
# pg_dump and psql use the same connection settings as before (PGPASSWORD /
# ~/.pgpass in the environment).
set -e
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
OUT_DIR="../../database/Backup"
DUMP_FILE="$OUT_DIR/microlims_$TIMESTAMP.dump"
DB_HOST=localhost
DB_USER=microlims_user
DB_NAME=microlims
mkdir -p "$OUT_DIR"

STARTED_AT=$(date -u +%Y-%m-%dT%H:%M:%SZ)
PG_EXIT=0
pg_dump -h "$DB_HOST" -U "$DB_USER" -d "$DB_NAME" -F c -f "$DUMP_FILE" || PG_EXIT=$?
FINISHED_AT=$(date -u +%Y-%m-%dT%H:%M:%SZ)

if [ "$PG_EXIT" -eq 0 ]; then
  SUCCEEDED=true
  ERROR_TEXT=""
  echo "Backup written to $DUMP_FILE"
else
  SUCCEEDED=false
  ERROR_TEXT="pg_dump failed with exit code $PG_EXIT"
fi

# Size only when the file exists; a failed attempt may never have created it.
SIZE_BYTES=""
if [ -f "$DUMP_FILE" ]; then
  SIZE_BYTES=$(stat -c %s "$DUMP_FILE" 2>/dev/null || stat -f %z "$DUMP_FILE" 2>/dev/null || true)
fi

# Values go in as psql variables and are quoted by psql (:'name'), never
# spliced into the SQL text.
psql -X -q -h "$DB_HOST" -U "$DB_USER" -d "$DB_NAME" \
  -v ON_ERROR_STOP=1 \
  -v started_at="$STARTED_AT" \
  -v finished_at="$FINISHED_AT" \
  -v succeeded="$SUCCEEDED" \
  -v file_name="$(basename "$DUMP_FILE")" \
  -v size_bytes="$SIZE_BYTES" \
  -v error_text="$ERROR_TEXT" \
  -v host_name="$(hostname)" <<'SQL' || echo "warning: could not record backup run in BackupRuns" >&2
INSERT INTO "BackupRuns" ("StartedAt", "FinishedAt", "Succeeded", "FileName", "SizeBytes", "Error", "Host")
VALUES (
  :'started_at'::timestamptz,
  :'finished_at'::timestamptz,
  :'succeeded'::boolean,
  :'file_name',
  NULLIF(:'size_bytes', '')::bigint,
  NULLIF(:'error_text', ''),
  :'host_name'
);
SQL

exit "$PG_EXIT"
