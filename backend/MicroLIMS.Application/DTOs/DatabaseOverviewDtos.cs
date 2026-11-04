namespace MicroLIMS.Application.DTOs;

// Raw figures as PostgreSQL reports them, before the service shapes them.
public sealed record DatabaseSizeSnapshot(
    long DatabaseSizeBytes,
    string DatabaseSizePretty,
    IReadOnlyList<TableSizeRow> Tables);

// One row of pg_total_relation_size, with the planner's row estimate.
// ReltuplesEstimate is pg_class.reltuples as stored: -1 means the table has
// never been analyzed.
public sealed record TableSizeRow(
    string Schema,
    string Name,
    long TotalSizeBytes,
    string TotalSizePretty,
    float ReltuplesEstimate);

public sealed record DatabaseOverviewDto(
    long DatabaseSizeBytes,
    string DatabaseSizePretty,
    IReadOnlyList<DatabaseTableSizeDto> TopTables,
    BackupRunDto? LatestBackup);

public sealed record DatabaseTableSizeDto(
    string Schema,
    string Name,
    long TotalSizeBytes,
    string TotalSizePretty,
    // Null when the planner has no estimate yet (reltuples = -1).
    long? EstimatedRows);

public sealed record BackupRunDto(
    long Id,
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    bool Succeeded,
    string FileName,
    long? SizeBytes,
    string? Error,
    string Host);
