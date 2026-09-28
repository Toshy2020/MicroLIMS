using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Seed;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// What the API does on first start against an empty database: apply every
// migration, then run the full seeder. The seeder used to add its two
// pieces of laboratory equipment without the laboratory section that
// Equipment.SectionId requires, so a fresh installation could not start.
public class FreshDatabaseSeedPostgresTests
{
    [PostgresFact]
    public async Task EmptyDatabase_MigratesAndSeeds_AndSeedingTwiceChangesNothing()
    {
        var admin = PostgresTestConfiguration.AdminConnectionString!;
        var name = PostgresTestConfiguration.NewTestDatabaseName();
        var connectionString = PostgresTestConfiguration.BuildTestConnectionString(admin, name);

        await ExecuteAsync(admin, $"CREATE DATABASE \"{name}\";");
        try
        {
            await using var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
                .UseNpgsql(connectionString).Options);
            await db.Database.MigrateAsync();

            DbSeeder.Seed(db);
            var equipmentCount = await db.Equipment.CountAsync();
            var inventoryCount = await db.EquipmentInventories.CountAsync();
            DbSeeder.Seed(db);

            var micro = await db.DocumentSections.SingleAsync(s => s.Code == "MICRO");
            var seeded = await db.Equipment.Where(e => e.Code == "INC-F-ML-F-01-002" || e.Code == "AUT-F-ML-F-03-045").ToListAsync();
            Assert.Equal(2, seeded.Count);
            Assert.All(seeded, e => Assert.Equal(micro.Id, e.SectionId));
            Assert.Contains(seeded, e => e.Type == EquipmentType.Autoclave);

            var inventory = await db.EquipmentInventories.Where(i => i.Code == "INC-F-ML-F-01-002" || i.Code == "AUT-F-ML-F-03-045").ToListAsync();
            Assert.All(inventory, i => Assert.Equal(micro.Id, i.SectionId));

            Assert.Equal(equipmentCount, await db.Equipment.CountAsync());
            Assert.Equal(inventoryCount, await db.EquipmentInventories.CountAsync());
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            PostgresTestConfiguration.EnsureSafeToDrop(name, admin);
            await ExecuteAsync(admin, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE);");
        }
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
