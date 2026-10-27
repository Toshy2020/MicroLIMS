using MicroLIMS.Application.DTOs;

namespace MicroLIMS.Application.Interfaces;

// Read-only catalog statistics for the admin Overview. Implemented in
// Persistence with raw SQL against pg_database_size and pg_total_relation_size;
// Application never sees the SQL.
public interface IDatabaseStatsQuery
{
    Task<DatabaseSizeSnapshot> GetSizeSnapshotAsync(int topTableCount, CancellationToken cancellationToken = default);
}
