namespace MicroLIMS.Domain.Entities;

// One database backup attempt, written by scripts/DatabaseBackup/backup.sh
// after every pg_dump, whether it succeeded or failed.
//
// Operational data only - NOT a GxP record. Written by the backup script
// with psql, so it never passes through CaptureAuditEntries; it is listed
// in the audit exclusions to keep that stated explicitly.
//
// Error holds the failure reason only, never a connection string or a
// password. The script records a fixed phrase plus the exit code.
public class BackupRun
{
    public long Id { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }

    public bool Succeeded { get; set; }

    // File name only, not a full path: the directory is a deployment detail.
    public string FileName { get; set; } = string.Empty;

    // Null when the dump file does not exist, e.g. the attempt failed before
    // pg_dump created it.
    public long? SizeBytes { get; set; }

    public string? Error { get; set; }

    public string Host { get; set; } = string.Empty;
}
