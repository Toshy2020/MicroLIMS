using MicroLIMS.Application.Abstractions.Persistence;
using System.Collections.Concurrent;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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

        var result = await ExecuteScalarAsync($"SELECT nextval('{sanitizedSequenceName}')", cancellationToken);
        if (result == null || result == DBNull.Value)
        {
            throw new InvalidOperationException($"Sequence '{sanitizedSequenceName}' returned null.");
        }

        return Convert.ToInt64(result);
    }

    public async Task<int> ReserveIdAsync<TEntity>(CancellationToken cancellationToken = default) where TEntity : class
    {
        var entityType = _db.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not part of the model.");
        var key = entityType.FindPrimaryKey();
        if (key is null || key.Properties.Count != 1 || key.Properties[0].ClrType != typeof(int))
            throw new InvalidOperationException($"{typeof(TEntity).Name} does not have a single integer key.");
        var keyProperty = key.Properties[0];

        // In-memory provider fallback for unit testing: it has no sequences.
        if (!_db.Database.IsRelational())
        {
            var highest = await _db.Set<TEntity>()
                .Select(e => (int?)EF.Property<int>(e, keyProperty.Name))
                .MaxAsync(cancellationToken);
            return (highest ?? 0) + 1;
        }

        var tableName = entityType.GetTableName()
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not mapped to a table.");
        var table = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
        var columnName = keyProperty.GetColumnName(table)
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name}'s key is not mapped to a column.");

        var qualifiedTable = entityType.GetSchema() is { } schema
            ? $"{QuoteIdentifier(schema)}.{QuoteIdentifier(tableName)}"
            : QuoteIdentifier(tableName);
        var result = await ExecuteScalarAsync(
            $"SELECT nextval(pg_get_serial_sequence({QuoteLiteral(qualifiedTable)}, {QuoteLiteral(columnName)}))",
            cancellationToken);
        if (result == null || result == DBNull.Value)
            throw new InvalidOperationException($"{typeof(TEntity).Name}'s key column has no identity sequence.");

        return Convert.ToInt32(result);
    }

    private static string QuoteIdentifier(string name) => $"\"{name.Replace("\"", "\"\"")}\"";

    private static string QuoteLiteral(string value) => $"'{value.Replace("'", "''")}'";

    // Runs inside the context's current transaction, when there is one.
    private async Task<object?> ExecuteScalarAsync(string commandText, CancellationToken cancellationToken)
    {
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

            command.CommandText = commandText;
            return await command.ExecuteScalarAsync(cancellationToken);
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
