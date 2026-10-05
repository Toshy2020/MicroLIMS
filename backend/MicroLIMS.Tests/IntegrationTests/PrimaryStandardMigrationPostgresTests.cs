using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Own throw-away database: migrate to the migration BEFORE PrimaryStandardType, seed old-schema rows, migrate on, then down.
public class PrimaryStandardMigrationPostgresTests
{
    private const string Before = "20261005083717_DropDeadSpecificationFields";
    private const string Target = "20261005100326_PrimaryStandardType";

    [PostgresFact]
    public async Task Migration_PromotesTitrantStandards_AndRevertsOnDown()
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
            // Non-superuser CI role: drop FKs while seeding, re-add NOT VALID (same as RetireCalibrationRunsMigrationTests).
            var fks = new List<(string Table, string Name, string Def)>();
            await using (var q = new NpgsqlCommand(
                "SELECT conrelid::regclass::text, conname, pg_get_constraintdef(oid) FROM pg_constraint " +
                "WHERE contype = 'f' AND connamespace = 'public'::regnamespace", c))
            await using (var r = await q.ExecuteReaderAsync())
                while (await r.ReadAsync()) fks.Add((r.GetString(0), r.GetString(1), r.GetString(2)));
            foreach (var fk in fks) await Exec(c, $"ALTER TABLE {fk.Table} DROP CONSTRAINT \"{fk.Name}\"");

            var khp = await Insert(c, "MaterialMasterEntries", ("Code", "RG-022"), ("Category", 0));
            var other = await Insert(c, "MaterialMasterEntries", ("Code", "RG-100"), ("Category", 0));
            await Insert(c, "SolutionMasters", ("Name", "0.1N NaOH"), ("Type", 2), ("StandardEntryId", khp));
            var khpLot = await Insert(c, "Materials", ("MaterialType", 6), ("MaterialMasterEntryId", khp));
            var otherLot = await Insert(c, "Materials", ("MaterialType", 6), ("MaterialMasterEntryId", other));

            foreach (var fk in fks) await Exec(c, $"ALTER TABLE {fk.Table} ADD CONSTRAINT \"{fk.Name}\" {fk.Def} NOT VALID");

            await migrator.MigrateAsync(Target);

            Assert.Equal(3, await Scalar(c, $"SELECT \"Category\" FROM \"MaterialMasterEntries\" WHERE \"Id\" = {khp}"));
            Assert.Equal(13, await Scalar(c, $"SELECT \"MaterialType\" FROM \"Materials\" WHERE \"Id\" = {khpLot}"));
            Assert.Equal(0, await Scalar(c, $"SELECT \"Category\" FROM \"MaterialMasterEntries\" WHERE \"Id\" = {other}"));
            Assert.Equal(6, await Scalar(c, $"SELECT \"MaterialType\" FROM \"Materials\" WHERE \"Id\" = {otherLot}"));

            await AssertAudit(c, "MaterialMasterEntry", khp, "{\"Category\":0}", "{\"Category\":3}");
            await AssertAudit(c, "Material", khpLot, "{\"MaterialType\":6}", "{\"MaterialType\":13}");
            Assert.Equal(2, await Scalar(c, "SELECT count(*) FROM \"AuditLogs\" WHERE \"ActorType\" = 2 AND \"SystemProcessName\" = 'Migration 20261005100326_PrimaryStandardType'"));

            await migrator.MigrateAsync(Before);

            await AssertAudit(c, "MaterialMasterEntry", khp, "{\"Category\":3}", "{\"Category\":0}");
            await AssertAudit(c, "Material", khpLot, "{\"MaterialType\":13}", "{\"MaterialType\":6}");
            Assert.Equal(4, await Scalar(c, "SELECT count(*) FROM \"AuditLogs\" WHERE \"ActorType\" = 2 AND \"SystemProcessName\" = 'Migration 20261005100326_PrimaryStandardType'"));

            Assert.Equal(0, await Scalar(c, $"SELECT \"Category\" FROM \"MaterialMasterEntries\" WHERE \"Id\" = {khp}"));
            Assert.Equal(6, await Scalar(c, $"SELECT \"MaterialType\" FROM \"Materials\" WHERE \"Id\" = {khpLot}"));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            PostgresTestConfiguration.EnsureSafeToDrop(name, admin);
            await Exec(admin, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
        }
    }

    // Inserts one row; every NOT NULL column without a default gets a type-based filler unless overridden.
    private static async Task<int> Insert(NpgsqlConnection c, string table, params (string Col, object Val)[] over)
    {
        var values = over.ToDictionary(o => o.Col, o => o.Val);
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

    private static async Task AssertAudit(NpgsqlConnection c, string entity, int id, string prev, string next) =>
        Assert.Equal(1, await Scalar(c,
            $"SELECT count(*) FROM \"AuditLogs\" WHERE \"EntityName\" = '{entity}' AND \"EntityId\" = '{id}' AND \"ActorType\" = 2 " +
            $"AND \"PreviousValue\" = '{prev}' AND \"NewValue\" = '{next}'"));

    private static async Task<long> Scalar(NpgsqlConnection c, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, c);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    private static async Task Exec(string cs, string sql)
    {
        await using var c = new NpgsqlConnection(cs);
        await c.OpenAsync();
        await Exec(c, sql);
    }

    private static async Task Exec(NpgsqlConnection c, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }
}
