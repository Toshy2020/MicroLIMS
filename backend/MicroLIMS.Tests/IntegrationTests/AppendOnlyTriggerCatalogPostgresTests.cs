using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// CLAUDE.md Rule 3.3.4 / 7.4 - post-migration verification. The fixture
// applies every committed migration to an empty database; this test then reads
// the PostgreSQL trigger catalogue and asserts that each append-only table still
// carries both of its protection triggers, enabled and of the right shape. A
// migration that rebuilds, renames or re-creates a protected table and forgets
// to re-create its triggers fails here, even before any row exists. The
// behavioural side (UPDATE/DELETE/TRUNCATE actually rejected) is covered per
// table by AppendOnlyAuditHardeningPostgresTests, AccessReviewPostgresTests and
// the other *PostgresTests suites.
[Collection("PostgresDatabaseCollection")]
public class AppendOnlyTriggerCatalogPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public AppendOnlyTriggerCatalogPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    // pg_trigger.tgtype bits (src/include/catalog/pg_trigger.h).
    private const int Row = 1 << 0, Before = 1 << 1, Insert = 1 << 2, Delete = 1 << 3, Update = 1 << 4, Truncate = 1 << 5;

    // Every protected table and the row events its _immutable trigger must
    // intercept. Adding a protected table means adding it here; the reverse
    // check below fails if a table gets an _immutable trigger without an entry.
    private static readonly IReadOnlyDictionary<string, int> ProtectedTables = new Dictionary<string, int>
    {
        ["AuditLogs"] = Update | Delete,
        ["AuditEventChanges"] = Update | Delete,
        ["ElectronicSignatures"] = Update | Delete,
        ["DocumentAcknowledgementRecords"] = Update | Delete,
        ["WorkflowProfileVersions"] = Update | Delete,
        ["InstrumentIngestPayloads"] = Update | Delete,
        ["InstrumentIngestVersions"] = Update | Delete,
        ["InstrumentIngests"] = Update | Delete,
        ["LoginHistories"] = Update | Delete,
        ["SecurityAuditEvents"] = Update | Delete,
        ["SystemSettingVersions"] = Update | Delete,
        ["AccessReviews"] = Update | Delete,
        // Findings may not even be inserted once their review is signed.
        ["AccessReviewFindings"] = Insert | Update | Delete,
    };

    private sealed record TriggerRow(string Table, string Name, int Type, char Enabled);

    [PostgresFact]
    public async Task EveryProtectedTable_HasItsAppendOnlyTriggers_AfterAllMigrations()
    {
        await using var db = _fixture.CreateDbContext();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        var problems = FindProblems(await ReadTriggersAsync(conn, transaction: null));

        Assert.True(problems.Count == 0, "Append-only trigger catalogue is incomplete:\n" + string.Join("\n", problems));
    }

    // Rule 4.7 self-check: the catalogue check must actually catch a disabled
    // or missing trigger. DDL is transactional in PostgreSQL, so both changes
    // are rolled back and never touch the shared test database.
    [PostgresFact]
    public async Task CatalogueCheck_DetectsDisabledAndDroppedTriggers()
    {
        await using var db = _fixture.CreateDbContext();
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            await ExecuteAsync(conn, tx, "ALTER TABLE \"AuditLogs\" DISABLE TRIGGER trg_auditlogs_immutable");
            await ExecuteAsync(conn, tx, "DROP TRIGGER trg_electronicsignatures_no_truncate ON \"ElectronicSignatures\"");

            var problems = FindProblems(await ReadTriggersAsync(conn, tx));

            Assert.Contains(problems, p => p.Contains("trg_auditlogs_immutable") && p.Contains("disabled"));
            Assert.Contains(problems, p => p.Contains("trg_electronicsignatures_no_truncate") && p.Contains("missing"));
            Assert.Equal(2, problems.Count);
        }
        finally
        {
            await tx.RollbackAsync();
        }

        // The rollback restored both triggers.
        Assert.Empty(FindProblems(await ReadTriggersAsync(conn, transaction: null)));
    }

    private static List<string> FindProblems(IReadOnlyList<TriggerRow> triggers)
    {
        var problems = new List<string>();

        foreach (var (table, rowEvents) in ProtectedTables)
        {
            var lower = table.ToLowerInvariant();
            CheckTrigger(problems, triggers, table, $"trg_{lower}_immutable", Row | Before | rowEvents, forbidden: Truncate);
            CheckTrigger(problems, triggers, table, $"trg_{lower}_no_truncate", Before | Truncate, forbidden: Row);
        }

        // Reverse check: a table protected in a migration must be listed above.
        foreach (var t in triggers.Where(t => t.Name.EndsWith("_immutable", StringComparison.Ordinal)))
        {
            if (!ProtectedTables.ContainsKey(t.Table))
                problems.Add($"{t.Table}: has append-only trigger {t.Name} but is not in ProtectedTables - add it.");
        }

        return problems;
    }

    private static void CheckTrigger(
        List<string> problems, IReadOnlyList<TriggerRow> triggers, string table, string name, int required, int forbidden)
    {
        var trigger = triggers.SingleOrDefault(t => t.Table == table && t.Name == name);
        if (trigger is null)
        {
            problems.Add($"{table}: trigger {name} is missing.");
            return;
        }
        // 'O' fires in origin (normal) mode, 'A' always; 'D' is disabled and
        // 'R' fires only in replica mode - both leave the table unprotected.
        if (trigger.Enabled is not ('O' or 'A'))
            problems.Add($"{table}: trigger {name} is disabled (tgenabled = '{trigger.Enabled}').");
        if ((trigger.Type & required) != required || (trigger.Type & forbidden) != 0)
            problems.Add($"{table}: trigger {name} has the wrong shape (tgtype = {trigger.Type}, expected bits {required}, none of {forbidden}).");
    }

    private static async Task<IReadOnlyList<TriggerRow>> ReadTriggersAsync(DbConnection conn, DbTransaction? transaction)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            SELECT c.relname, t.tgname, t.tgtype::int, t.tgenabled
            FROM pg_trigger t
            JOIN pg_class c ON c.oid = t.tgrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE NOT t.tgisinternal AND n.nspname = current_schema()
            """;

        var rows = new List<TriggerRow>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add(new TriggerRow(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetChar(3)));
        return rows;
    }

    private static async Task ExecuteAsync(DbConnection conn, DbTransaction tx, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}
