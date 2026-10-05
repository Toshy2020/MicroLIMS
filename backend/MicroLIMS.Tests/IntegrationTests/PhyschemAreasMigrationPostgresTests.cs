using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Own throw-away database: stop before PhyschemAreas, seed old-schema rows, migrate on, then back down.
public class PhyschemAreasMigrationPostgresTests
{
    private const string Before = "20261005083717_DropDeadSpecificationFields";
    private const string Target = "20261005094824_PhyschemAreas";

    [PostgresFact]
    public async Task Migration_BackfillsTestAreasFromSpecifications_AndDownDropsColumns()
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
            // CI's role is not a superuser: drop FKs while seeding, re-add NOT VALID.
            var fks = new List<(string Table, string Name, string Def)>();
            await using (var q = new NpgsqlCommand(
                "SELECT conrelid::regclass::text, conname, pg_get_constraintdef(oid) FROM pg_constraint " +
                "WHERE contype = 'f' AND connamespace = 'public'::regnamespace", c))
            await using (var r = await q.ExecuteReaderAsync())
                while (await r.ReadAsync()) fks.Add((r.GetString(0), r.GetString(1), r.GetString(2)));
            foreach (var fk in fks) await Exec(c, $"ALTER TABLE {fk.Table} DROP CONSTRAINT \"{fk.Name}\"");

            // The sections may already be seeded by earlier migrations; use whatever FP / non-FP exists.
            var fp = await Section(c, "FP");
            var micro = await Section(c, "MICRO");
            var fpItem = await Insert(c, "Items", ("Code", "I-FP"), ("Category", 0));
            var rmItem = await Insert(c, "Items", ("Code", "I-RM"), ("Category", 1));
            foreach (var code in new[] { "T-FP", "T-RM", "T-BOTH", "T-NONE" })
                await Insert(c, "TestDefinitions", ("Code", code), ("SectionId", fp));
            await Insert(c, "TestDefinitions", ("Code", "T-MIC"), ("SectionId", micro));
            await Insert(c, "Specifications", ("TestCode", "T-FP"), ("ItemId", fpItem));
            await Insert(c, "Specifications", ("TestCode", "T-RM"), ("ItemId", rmItem));
            await Insert(c, "Specifications", ("TestCode", "T-BOTH"), ("ItemId", fpItem));
            await Insert(c, "Specifications", ("TestCode", "T-BOTH"), ("ItemId", rmItem));
            await Insert(c, "UserOrgMemberships", ("SectionId", fp));

            foreach (var fk in fks) await Exec(c, $"ALTER TABLE {fk.Table} ADD CONSTRAINT \"{fk.Name}\" {fk.Def} NOT VALID");

            await migrator.MigrateAsync(Target);

            Assert.Equal(0L, await Area(c, "T-FP"));
            Assert.Equal(1L, await Area(c, "T-RM"));
            Assert.Equal(2L, await Area(c, "T-BOTH"));
            Assert.Equal(0L, await Area(c, "T-NONE"));
            Assert.Null(await Area(c, "T-MIC"));
            Assert.Equal(0L, await Scalar(c, "SELECT count(*) FROM \"UserOrgMemberships\" WHERE \"PhyschemArea\" IS NOT NULL"));

            await migrator.MigrateAsync(Before);
            Assert.Equal(0L, await Scalar(c, "SELECT count(*) FROM information_schema.columns WHERE column_name = 'PhyschemArea'"));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            PostgresTestConfiguration.EnsureSafeToDrop(name, admin);
            await Exec(admin, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
        }
    }

    private static async Task<int> Section(NpgsqlConnection c, string code)
    {
        var id = await Scalar(c, $"SELECT COALESCE(MAX(\"Id\"), 0) FROM \"DocumentSections\" WHERE \"Code\" = '{code}'");
        return id > 0 ? (int)id : await Insert(c, "DocumentSections", ("Code", code));
    }

    private static async Task<object?> Area(NpgsqlConnection c, string code)
    {
        await using var cmd = new NpgsqlCommand($"SELECT \"PhyschemArea\" FROM \"TestDefinitions\" WHERE \"Code\" = '{code}'", c);
        var v = await cmd.ExecuteScalarAsync();
        return v is null or DBNull ? null : Convert.ToInt64(v);
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
