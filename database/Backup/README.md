# Database Backups

Store pg_dump backups here, or point your backup schedule (see
scripts/DatabaseBackup) at this folder. Never commit real production
backups containing patient/product data to source control - use
.gitignore or a separate encrypted storage location.
