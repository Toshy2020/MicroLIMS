using System.Data;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using Npgsql;

namespace MicroLIMS.Persistence.Diagnostics;

// Catalog statistics for the admin Overview. Read-only: two SELECTs against
// the system catalogs, no DDL and no writes. Runs on the request's own
// connection, the same way DatabaseHealthMonitorWorker reads pg_stat_*.
public sealed class DatabaseStatsQuery : IDatabaseStatsQuery
{
    private const string DatabaseSizeSql =
        "SELECT pg_database_size(current_database()), pg_size_pretty(pg_database_size(current_database()))";

    // Ordinary and materialized tables outside the system schemas. Partitioned
    // parents (relkind 'p') are left out: their own size is zero, and their
    // partitions are listed in their place.
    private const string TableSizeSql =
        "SELECT n.nspname, c.relname, " +
        "pg_total_relation_size(c.oid), pg_size_pretty(pg_total_relation_size(c.oid)), c.reltuples " +
        "FROM pg_class c " +
        "JOIN pg_namespace n ON n.oid = c.relnamespace " +
        "WHERE c.relkind IN ('r', 'm') " +
        "AND n.nspname NOT IN ('pg_catalog', 'information_schema') " +
        "AND n.nspname NOT LIKE 'pg_toast%' " +
        "ORDER BY pg_total_relation_size(c.oid) DESC " +
        "LIMIT @topCount";

    private readonly IMicroLimsDbContext _db;

    public DatabaseStatsQuery(IMicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<DatabaseSizeSnapshot> GetSizeSnapshotAsync(int topTableCount, CancellationToken cancellationToken = default)
    {
        var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        long sizeBytes;
        string sizePretty;

        await using (var command = new NpgsqlCommand(DatabaseSizeSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
            sizeBytes = reader.GetInt64(0);
            sizePretty = reader.GetString(1);
        }

        var tables = new List<TableSizeRow>();

        await using (var command = new NpgsqlCommand(TableSizeSql, connection))
        {
            command.Parameters.AddWithValue("topCount", topTableCount);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                tables.Add(new TableSizeRow(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetInt64(2),
                    reader.GetString(3),
                    reader.GetFloat(4)));
            }
        }

        return new DatabaseSizeSnapshot(sizeBytes, sizePretty, tables);
    }
}
