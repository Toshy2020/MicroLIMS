using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Own throw-away database: migrate to the migration BEFORE ReferenceOrganisms, seed old-schema organisms, lyophilized
// lots and cryovials, migrate on, and check the audited backfill (strains, lot and cryovial links; snapshots untouched).
public class ReferenceOrganismsBackfillPostgresTests
{
    private const string Before = "20261028114334_MaterialCategoriesAndOtherMaterials";
    private const string Target = "20261028123607_ReferenceOrganisms";
    private const string Process = "Migration " + Target;

    [PostgresFact]
    public async Task Migration_CreatesStrains_LinksLotsAndCryovials_AndAuditsEverything()
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

            // Organisms: three with an ATCC number (one padded), two without. Numbering follows the scientific name.
            var ecoli = await Insert(c, "Organisms", ("ScientificName", "Escherichia coli"), ("AtccNumber", "8739"));
            var staph = await Insert(c, "Organisms", ("ScientificName", "Staphylococcus aureus"), ("AtccNumber", " 6538 "));
            var pseudo = await Insert(c, "Organisms", ("ScientificName", "Pseudomonas aeruginosa"), ("AtccNumber", "9027"));
            var candida = await Insert(c, "Organisms", ("ScientificName", "Candida albicans"), ("AtccNumber", null));
            var aspergillus = await Insert(c, "Organisms", ("ScientificName", "Aspergillus brasiliensis"), ("AtccNumber", null));

            var byOrganism = await Lot(c, micro, "B1", organism: ecoli, atcc: "8739");
            var byAtcc = await Lot(c, micro, "B2", atcc: "atcc 6538");
            var otherA = await Lot(c, micro, "B3", organism: candida);
            var otherB = await Lot(c, micro, "B4", organism: candida);
            var noData = await Lot(c, micro, "B5");
            // The organism wins over the ATCC number of another species: an organism without a strain gets an Other strain.
            var organismWins = await Lot(c, micro, "B6", organism: aspergillus, atcc: "9027");
            var unknownAtcc = await Lot(c, micro, "B7", atcc: "ATCC 7777");
            var media = await Lot(c, micro, "B8", type: 0);

            var cryLinked = await Cryo(c, byOrganism, ecoli, "ECO/01/26");
            var cryOther = await Cryo(c, otherA, candida, "CAL/01/26");
            var cryUnlinkedLot = await Cryo(c, noData, ecoli, "MYS/01/26");
            var cryOtherSpecies = await Cryo(c, byOrganism, staph, "ECO/02/26");

            await migrator.MigrateAsync(Target);

            // Strains: ORG-001.. for the ATCC organisms by name (Escherichia, Pseudomonas, Staphylococcus), then Other strains.
            Assert.Equal(5, await Scalar(c, "SELECT count(*) FROM \"ReferenceOrganisms\""));
            Assert.Equal("ORG-001", await Text(c, $"SELECT \"Code\" FROM \"ReferenceOrganisms\" WHERE \"OrganismId\" = {ecoli}"));
            Assert.Equal("ORG-002", await Text(c, $"SELECT \"Code\" FROM \"ReferenceOrganisms\" WHERE \"OrganismId\" = {pseudo}"));
            Assert.Equal("ORG-003", await Text(c, $"SELECT \"Code\" FROM \"ReferenceOrganisms\" WHERE \"OrganismId\" = {staph}"));
            Assert.Equal("6538", await Text(c, $"SELECT \"StrainNumber\" FROM \"ReferenceOrganisms\" WHERE \"OrganismId\" = {staph}"));
            Assert.Equal(3, await Scalar(c, "SELECT count(*) FROM \"ReferenceOrganisms\" WHERE \"CultureCollection\" = 0 AND \"IsActive\" AND \"OwnerSectionId\" = " + micro));
            Assert.Equal(2, await Scalar(c, "SELECT count(*) FROM \"ReferenceOrganisms\" WHERE \"CultureCollection\" = 5 AND \"StrainNumber\" IS NULL AND \"Code\" IN ('ORG-004', 'ORG-005')"));

            // Lots: by organism, by ATCC number (also sets the missing organism), to an Other strain (one per organism).
            Assert.Equal(await StrainOf(c, ecoli), await LotStrain(c, byOrganism));
            Assert.Equal(await StrainOf(c, staph), await LotStrain(c, byAtcc));
            Assert.Equal(staph, await Scalar(c, $"SELECT \"OrganismId\" FROM \"Materials\" WHERE \"Id\" = {byAtcc}"));
            var candidaStrain = await LotStrain(c, otherA);
            Assert.Equal(candidaStrain, await LotStrain(c, otherB));
            Assert.Equal(5, await Scalar(c, $"SELECT \"CultureCollection\" FROM \"ReferenceOrganisms\" WHERE \"Id\" = {candidaStrain}"));
            var aspergillusStrain = await LotStrain(c, organismWins);
            Assert.Equal(5, await Scalar(c, $"SELECT \"CultureCollection\" FROM \"ReferenceOrganisms\" WHERE \"Id\" = {aspergillusStrain} AND \"OrganismId\" = {aspergillus}"));
            Assert.NotEqual(candidaStrain, aspergillusStrain);
            Assert.Equal(0, await Scalar(c, $"SELECT count(\"ReferenceOrganismId\") FROM \"Materials\" WHERE \"Id\" IN ({noData}, {unknownAtcc}, {media})"));

            // Snapshots are untouched.
            Assert.Equal("Lot B2", await Text(c, $"SELECT \"MaterialName\" FROM \"Materials\" WHERE \"Id\" = {byAtcc}"));
            Assert.Equal("atcc 6538", await Text(c, $"SELECT \"AtccNumber\" FROM \"Materials\" WHERE \"Id\" = {byAtcc}"));
            Assert.Equal("CODE-B1", await Text(c, $"SELECT \"Code\" FROM \"Materials\" WHERE \"Id\" = {byOrganism}"));
            Assert.Equal("ECO/01/26", await Text(c, $"SELECT \"Code\" FROM \"Cryovials\" WHERE \"Id\" = {cryLinked}"));

            // Cryovials take their lot's strain when the species agrees.
            Assert.Equal(await StrainOf(c, ecoli), await CryoStrain(c, cryLinked));
            Assert.Equal(candidaStrain, await CryoStrain(c, cryOther));
            Assert.Equal(0, await Scalar(c, $"SELECT count(\"ReferenceOrganismId\") FROM \"Cryovials\" WHERE \"Id\" IN ({cryUnlinkedLot}, {cryOtherSpecies})"));

            // One audit row per strain, per linked lot and per linked cryovial, plus the summary with the unlinked ids.
            Assert.Equal(5, await Audits(c, "ReferenceOrganism"));
            Assert.Equal(5, await Audits(c, "Material"));
            Assert.Equal(2, await Audits(c, "Cryovial"));
            var summary = await Text(c, $"SELECT \"NewValue\" FROM \"AuditLogs\" WHERE \"SystemProcessName\" = '{Process}' AND \"EntityName\" = 'ReferenceOrganismBackfill'");
            Assert.Contains("\"LotsUnlinkedCount\": 2", summary);
            Assert.Contains($"[{noData}, {unknownAtcc}]", summary);
            Assert.Contains("\"CryovialsLinked\": 2", summary);
            Assert.Contains($"[{cryUnlinkedLot}, {cryOtherSpecies}]", summary);

            // Existing media products stay selectable.
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM \"MediaProducts\" WHERE NOT \"IsActive\""));
        }
        finally
        {
            await ScratchPostgresDatabase.DropAsync(admin, name);
        }
    }

    private static Task<int> Lot(NpgsqlConnection c, int section, string batch, int type = 1, int? organism = null, string? atcc = null) =>
        Insert(c, "Materials", ("SectionId", section), ("MaterialType", type), ("MaterialName", "Lot " + batch), ("BatchNumber", batch),
            ("Code", "CODE-" + batch), ("OrganismId", organism), ("AtccNumber", atcc), ("Unit", 0));

    private static Task<int> Cryo(NpgsqlConnection c, int lotId, int organismId, string code) =>
        Insert(c, "Cryovials", ("MaterialId", lotId), ("OrganismId", organismId), ("Code", code), ("OrganismNameSnapshot", "x"));

    private static async Task<long> StrainOf(NpgsqlConnection c, int organismId) =>
        await Scalar(c, $"SELECT \"Id\" FROM \"ReferenceOrganisms\" WHERE \"OrganismId\" = {organismId} AND \"CultureCollection\" = 0");

    private static Task<long> LotStrain(NpgsqlConnection c, int lotId) =>
        Scalar(c, $"SELECT \"ReferenceOrganismId\" FROM \"Materials\" WHERE \"Id\" = {lotId}");

    private static Task<long> CryoStrain(NpgsqlConnection c, int cryoId) =>
        Scalar(c, $"SELECT \"ReferenceOrganismId\" FROM \"Cryovials\" WHERE \"Id\" = {cryoId}");

    private static Task<long> Audits(NpgsqlConnection c, string entity) =>
        Scalar(c, $"SELECT count(*) FROM \"AuditLogs\" WHERE \"SystemProcessName\" = '{Process}' AND \"EntityName\" = '{entity}'");

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
