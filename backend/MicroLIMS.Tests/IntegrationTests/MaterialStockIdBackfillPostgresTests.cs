using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Own throw-away database: migrate to the migration BEFORE MaterialStockIds, seed lots in every state, migrate on,
// and check that only in-stock lots were numbered (in receiving-date order), that the audit rows and the summary
// are written, and that the sequence and unique index carry on afterwards.
public class MaterialStockIdBackfillPostgresTests
{
    private const string Before = "20261028130815_StepMediaDiluentMediaProduct";
    private const string Target = "20261028140351_MaterialStockIds";
    private const string Process = "Migration " + Target;

    [PostgresFact]
    public async Task Migration_NumbersInStockLotsInReceivingOrder_LeavesExpiredAndDepletedAlone_AndAuditsEverything()
    {
        var admin = PostgresTestConfiguration.AdminConnectionString!;
        var name = PostgresTestConfiguration.NewTestDatabaseName();
        var cs = PostgresTestConfiguration.BuildTestConnectionString(admin, name);
        await Exec(admin, $"CREATE DATABASE \"{name}\"");
        try
        {
            await using var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>().UseNpgsql(cs).Options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(Before);

            await using var c = new NpgsqlConnection(cs);
            await c.OpenAsync();
            var micro = (int)await Scalar(c, "SELECT \"Id\" FROM \"DocumentSections\" WHERE \"Code\" = 'MICRO'");

            var future = DateTime.UtcNow.AddYears(1);
            // Inserted out of receiving order on purpose: numbering follows ReceivingDate, then Id.
            var laterA = await Lot(c, micro, "A", Utc(2025, 3, 1), future, 5m);
            var earlierNoExpiry = await Lot(c, micro, "B", Utc(2024, 1, 10), null, 2m);
            var laterSameDay = await Lot(c, micro, "C", Utc(2025, 3, 1), future, 1m);
            var expired = await Lot(c, micro, "D", Utc(2023, 5, 5), DateTime.UtcNow.AddDays(-3), 4m);
            var depleted = await Lot(c, micro, "E", Utc(2024, 6, 6), future, 0m);

            await migrator.MigrateAsync(Target);

            Assert.Equal("STK-2024-00001", await Text(c, StockIdOf(earlierNoExpiry)));
            Assert.Equal("STK-2025-00002", await Text(c, StockIdOf(laterA)));
            Assert.Equal("STK-2025-00003", await Text(c, StockIdOf(laterSameDay)));
            Assert.Equal(0, await Scalar(c, $"SELECT count(\"StockId\") FROM \"Materials\" WHERE \"Id\" IN ({expired}, {depleted})"));

            Assert.Equal(3, await Scalar(c, $"SELECT count(*) FROM \"AuditLogs\" WHERE \"SystemProcessName\" = '{Process}' AND \"EntityName\" = 'Material'"));
            var audit = await Text(c, $"SELECT \"NewValue\" FROM \"AuditLogs\" WHERE \"SystemProcessName\" = '{Process}' AND \"EntityId\" = '{earlierNoExpiry}'");
            Assert.Contains("STK-2024-00001", audit);
            var summary = await Text(c, $"SELECT \"NewValue\" FROM \"AuditLogs\" WHERE \"SystemProcessName\" = '{Process}' AND \"EntityName\" = 'MaterialStockIdBackfill'");
            Assert.Contains("\"LotsAssigned\": 3", summary);
            Assert.Contains("\"FirstStockId\": \"STK-2024-00001\"", summary);
            Assert.Contains("\"LastStockId\": \"STK-2025-00003\"", summary);
            Assert.Contains("\"LotsLeftWithoutStockId\": 2", summary);

            // The application continues the same sequence, and the unique index rejects a repeated ID but not several NULLs.
            Assert.Equal(4, await Scalar(c, "SELECT nextval('stock_id_seq')"));
            await Lot(c, micro, "F", Utc(2026, 1, 1), future, 1m);
            await Lot(c, micro, "G", Utc(2026, 1, 1), future, 1m);
            await Assert.ThrowsAsync<PostgresException>(() =>
                Exec(cs, $"UPDATE \"Materials\" SET \"StockId\" = 'STK-2024-00001' WHERE \"BatchNumber\" = 'F'"));
        }
        finally
        {
            await ScratchPostgresDatabase.DropAsync(admin, name);
        }
    }

    private static DateTime Utc(int y, int m, int d) => new(y, m, d, 12, 0, 0, DateTimeKind.Utc);

    private static string StockIdOf(int id) => $"SELECT \"StockId\" FROM \"Materials\" WHERE \"Id\" = {id}";

    private static Task<int> Lot(NpgsqlConnection c, int section, string batch, DateTime receiving, DateTime? expiry, decimal remaining) =>
        Insert(c, "Materials", ("SectionId", section), ("MaterialType", 0), ("MaterialName", "Lot " + batch), ("BatchNumber", batch),
            ("ReceivingDate", receiving), ("ExpiryDate", expiry), ("QuantityRemaining", remaining), ("Unit", 0));

    // Inserts one row; every NOT NULL column without a default gets a type-based filler unless overridden (null overrides are skipped).
    private static async Task<int> Insert(NpgsqlConnection c, string table, params (string Col, object? Val)[] over)
    {
        var values = over.Where(o => o.Val is not null).ToDictionary(o => o.Col, o => o.Val!);
        await using (var q = new NpgsqlCommand(
            "SELECT column_name, data_type FROM information_schema.columns WHERE table_name = @t AND is_nullable = 'NO' " +
            "AND column_default IS NULL AND is_identity = 'NO'", c))
        {
            q.Parameters.AddWithValue("t", table);
            await using var r = await q.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var col = r.GetString(0);
                if (values.ContainsKey(col)) continue;
                values[col] = r.GetString(1) switch
                {
                    "integer" or "bigint" or "smallint" => 1,
                    "numeric" => 1m,
                    "boolean" => false,
                    "timestamp with time zone" => DateTime.UtcNow,
                    "timestamp without time zone" => DateTime.UtcNow,
                    "date" => DateOnly.FromDateTime(DateTime.UtcNow),
                    "jsonb" or "json" => "{}",
                    _ => "x"
                };
            }
        }
        var cols = string.Join(",", values.Keys.Select(k => $"\"{k}\""));
        var ps = string.Join(",", values.Keys.Select((_, i) => $"@p{i}"));
        await using var cmd = new NpgsqlCommand($"INSERT INTO \"{table}\" ({cols}) VALUES ({ps}) RETURNING \"Id\"", c);
        var n = 0;
        foreach (var v in values.Values) cmd.Parameters.AddWithValue($"p{n++}", v);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<long> Scalar(NpgsqlConnection c, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, c);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    private static async Task<string> Text(NpgsqlConnection c, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, c);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task Exec(string cs, string sql)
    {
        await using var c = new NpgsqlConnection(cs);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }
}
