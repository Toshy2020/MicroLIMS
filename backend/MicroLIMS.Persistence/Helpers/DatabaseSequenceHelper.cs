using System.Collections.Concurrent;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Persistence.Helpers;

public class DatabaseSequenceHelper : IDatabaseSequenceHelper
{
    private readonly MicroLimsDbContext _db;
    private static readonly ConcurrentDictionary<string, long> _inMemorySequences = new(StringComparer.OrdinalIgnoreCase);

    public DatabaseSequenceHelper(MicroLimsDbContext db)
    {
        _db = db;
    }

    public async Task<long> GetNextSequenceValueAsync(string sequenceName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sequenceName))
            throw new ArgumentException("Sequence name cannot be empty.", nameof(sequenceName));

        // In-memory provider fallback for unit testing
        if (!_db.Database.IsRelational())
        {
            return _inMemorySequences.AddOrUpdate(sequenceName, 1, (_, current) => current + 1);
        }

        var sanitizedSequenceName = new string(sequenceName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrWhiteSpace(sanitizedSequenceName))
            throw new ArgumentException($"Invalid sequence name: '{sequenceName}'", nameof(sequenceName));

        var connection = _db.Database.GetDbConnection();
        var wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            var currentTx = _db.Database.CurrentTransaction;
            if (currentTx != null)
            {
                command.Transaction = currentTx.GetDbTransaction();
            }

            command.CommandText = $"SELECT nextval('{sanitizedSequenceName}')";
            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result == null || result == DBNull.Value)
            {
                throw new InvalidOperationException($"Sequence '{sanitizedSequenceName}' returned null.");
            }

            return Convert.ToInt64(result);
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync();
            }
        }
    }
}
