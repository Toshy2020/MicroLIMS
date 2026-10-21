using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Migrations;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Own throw-away database: migrate to the migration BEFORE AddWorkflowProfileVersions, seed
// legacy rows, migrate on, then down. The ids are looked up, so the test survives EF's timestamp.
public class WorkflowProfileMigrationPostgresTests
{
    [PostgresFact]
    public async Task Migration_ImportsLegacyWeightsAsDrafts_InstallsTheTrigger_AndRevertsOnDown()
    {
        var admin = PostgresTestConfiguration.AdminConnectionString!;
        var name = PostgresTestConfiguration.NewTestDatabaseName();
        var cs = PostgresTestConfiguration.BuildTestConnectionString(admin, name);
        await Exec(admin, $"CREATE DATABASE \"{name}\"");
        try
        {
            await using var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>().UseNpgsql(cs).Options);
            var ids = db.GetService<IMigrationsAssembly>().Migrations.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            var target = ids.Single(k => k.EndsWith("_AddWorkflowProfileVersions", StringComparison.Ordinal));
            var before = ids[ids.IndexOf(target) - 1];
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(before);

            await using var c = new NpgsqlConnection(cs);
            await c.OpenAsync();
            // Non-superuser CI role: drop FKs while seeding, re-add NOT VALID afterwards.
            var fks = new List<(string Table, string Name, string Def)>();
            await using (var q = new NpgsqlCommand(
                "SELECT conrelid::regclass::text, conname, pg_get_constraintdef(oid) FROM pg_constraint " +
                "WHERE contype = 'f' AND connamespace = 'public'::regnamespace", c))
            await using (var r = await q.ExecuteReaderAsync())
                while (await r.ReadAsync()) fks.Add((r.GetString(0), r.GetString(1), r.GetString(2)));
            foreach (var fk in fks) await Exec(c, $"ALTER TABLE {fk.Table} DROP CONSTRAINT \"{fk.Name}\"");

            await Insert(c, "Roles", ("Name", "WP Admin"), ("Type", 0));
            await Insert(c, "Roles", ("Name", "WP Section Head"), ("Type", 1));
            await Insert(c, "Roles", ("Name", "WP Analyst"), ("Type", 3));

            // WorkflowType ints: 0 CountTest, 2 HplcAssay (retired), 4 Measurement.
            await Insert(c, "TestDefinitions", ("Code", "TAMC"), ("DisplayName", "TAMC"), ("WorkflowType", 0));
            await Insert(c, "TestDefinitions", ("Code", "HPLCOLD"), ("DisplayName", "Old HPLC"), ("WorkflowType", 2));
            await Insert(c, "TestDefinitions", ("Code", "PH"), ("DisplayName", "pH"), ("WorkflowType", 4));
            await Insert(c, "WorkloadWeights", ("TestCode", "TAMC"), ("Weight", 1.5m));
            await Insert(c, "WorkloadWeights", ("TestCode", "HPLCOLD"), ("Weight", 2.0m));
            await Insert(c, "WorkloadWeights", ("TestCode", "PH"), ("Weight", 0.5m));
            await Insert(c, "WorkloadWeights", ("TestCode", "INVESTIGATION"), ("Weight", 1.0m)); // no test definition

            foreach (var fk in fks) await Exec(c, $"ALTER TABLE {fk.Table} ADD CONSTRAINT \"{fk.Name}\" {fk.Def} NOT VALID");

            await migrator.MigrateAsync(target);

            // Legacy import: two Drafts, with no effect until signed.
            Assert.Equal(2, await Scalar(c, "SELECT count(*) FROM \"WorkflowProfileVersions\" WHERE \"Status\" = 0 AND \"CreatedByUserId\" = 0 AND \"ElectronicSignatureId\" IS NULL"));
            Assert.Equal(1, await Scalar(c, "SELECT count(*) FROM \"WorkflowProfileVersions\" WHERE \"TestCode\" = 'TAMC' AND \"WorkflowType\" = 0 AND \"WorkUnits\" = 1.5 AND \"TargetAnalystDays\" IS NULL AND \"EffectiveFrom\" = CURRENT_DATE AND \"ReasonForChange\" = 'Imported from legacy workload weight'"));
            Assert.Equal(1, await Scalar(c, "SELECT count(*) FROM \"WorkflowProfileVersions\" WHERE \"TestCode\" = 'PH' AND \"WorkflowType\" = 4 AND \"WorkUnits\" = 0.5"));
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM \"WorkflowProfileVersions\" WHERE \"TestCode\" IN ('HPLCOLD', 'INVESTIGATION')"));
            Assert.Equal(2, await Scalar(c, $"SELECT count(*) FROM \"AuditLogs\" WHERE \"ActorType\" = 2 AND \"ActionCode\" = 'WorkflowProfileDraftImported' AND \"SystemProcessName\" = 'Migration {target}'"));

            // Permission: row enforced, granted to role types 0 and 1 only.
            Assert.Equal(1, await Scalar(c, "SELECT count(*) FROM \"Permissions\" WHERE \"Code\" = 'Kpi.ConfigureProfiles' AND \"IsEnforced\""));
            Assert.Equal(2, await Scalar(c, "SELECT count(*) FROM \"RolePermissions\" rp JOIN \"Permissions\" p ON p.\"Id\" = rp.\"PermissionId\" WHERE p.\"Code\" = 'Kpi.ConfigureProfiles'"));

            // The permission SQL is idempotent: replaying it changes nothing.
            foreach (var op in new AddWorkflowProfileVersions().UpOperations.OfType<SqlOperation>().Where(o => o.Sql.Contains("Kpi.ConfigureProfiles")))
                await Exec(c, op.Sql);
            Assert.Equal(1, await Scalar(c, "SELECT count(*) FROM \"Permissions\" WHERE \"Code\" = 'Kpi.ConfigureProfiles'"));
            Assert.Equal(2, await Scalar(c, "SELECT count(*) FROM \"RolePermissions\" rp JOIN \"Permissions\" p ON p.\"Id\" = rp.\"PermissionId\" WHERE p.\"Code\" = 'Kpi.ConfigureProfiles'"));

            // Trigger: a Draft may be signed in place once, an Effective row never changes, nothing is deleted.
            await Exec(c, "UPDATE \"WorkflowProfileVersions\" SET \"Status\" = 1 WHERE \"TestCode\" = 'TAMC'");
            var updateEx = await Assert.ThrowsAsync<PostgresException>(
                () => Exec(c, "UPDATE \"WorkflowProfileVersions\" SET \"WorkUnits\" = 9 WHERE \"TestCode\" = 'TAMC'"));
            Assert.Contains("append-only", updateEx.MessageText);
            var deleteEffective = await Assert.ThrowsAsync<PostgresException>(
                () => Exec(c, "DELETE FROM \"WorkflowProfileVersions\" WHERE \"TestCode\" = 'TAMC'"));
            Assert.Contains("append-only", deleteEffective.MessageText);
            var deleteDraft = await Assert.ThrowsAsync<PostgresException>(
                () => Exec(c, "DELETE FROM \"WorkflowProfileVersions\" WHERE \"TestCode\" = 'PH'"));
            Assert.Contains("append-only", deleteDraft.MessageText);

            // The check constraints back up the service validation.
            await Assert.ThrowsAsync<PostgresException>(() => Exec(c,
                "INSERT INTO \"WorkflowProfileVersions\" (\"WorkflowType\", \"EffectiveFrom\", \"Status\", \"ReasonForChange\", \"CreatedByUserId\", \"CreatedAt\") " +
                "VALUES (0, CURRENT_DATE, 0, 'no values', 1, now())"));

            await migrator.MigrateAsync(before);

            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM information_schema.tables WHERE table_name = 'WorkflowProfileVersions'"));
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM pg_proc WHERE proname = 'prevent_workflowprofileversion_modification'"));
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM \"Permissions\" WHERE \"Code\" = 'Kpi.ConfigureProfiles'"));
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM \"RolePermissions\" rp JOIN \"Permissions\" p ON p.\"Id\" = rp.\"PermissionId\" WHERE p.\"Code\" = 'Kpi.ConfigureProfiles'"));
            Assert.Equal(4, await Scalar(c, "SELECT count(*) FROM \"WorkloadWeights\"")); // legacy tables untouched
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            PostgresTestConfiguration.EnsureSafeToDrop(name, admin);
            await DropWithRetry(admin, name);
        }
    }

    // WITH (FORCE) cannot terminate a backend the CI role does not own (autovacuum, a straggling pooled
    // session), which fails with 42501. Retry briefly; a leftover uniquely named database is harmless
    // and must not fail an otherwise green test.
    private static async Task DropWithRetry(string admin, string name)
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                await Exec(admin, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
                return;
            }
            catch (PostgresException ex) when (ex.SqlState == "42501" || ex.SqlState == "55006")
            {
                if (attempt == 5) return;
                await Task.Delay(500 * attempt);
            }
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

// Real transactions and real xmin: the half of the sign-and-mutate rules InMemory cannot show.
[Collection("PostgresDatabaseCollection")]
public class WorkflowProfileServicePostgresTests
{
    private const string Password = "Correct-Password-1!";
    private readonly PostgresTestFixture _fixture;

    public WorkflowProfileServicePostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static async Task<User> SeedUserAsync(MicroLimsDbContext db)
    {
        var tag = Guid.NewGuid().ToString("N");
        var role = new Role { Type = RoleType.SectionHead, Name = "WP SH " + tag };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var user = new User { FullName = "WP Signer", Username = "wp" + tag, RoleId = role.Id, PasswordHash = TestPasswords.Hash(Password) };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static DateOnly Tomorrow => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

    [PostgresFact]
    public async Task AddVersion_WrongPassword_LeavesNoRowSignatureOrAuditEvent()
    {
        await using var db = _fixture.CreateDbContext();
        var user = await SeedUserAsync(db);
        var reason = "marker-" + Guid.NewGuid();
        var failedBefore = await db.AuditLogs.CountAsync(a => a.Action == "SignatureFailed");

        await Assert.ThrowsAsync<SignatureVerificationException>(() => TestServiceFactory.WorkflowProfile(db).AddVersionAsync(
            new CreateWorkflowProfileVersionRequest(WorkflowType.WeightVariation, null, 1m, 5m, Tomorrow, reason, "WrongPassword!"),
            user.Id, "127.0.0.1"));

        await using var verify = _fixture.CreateDbContext();
        Assert.False(await verify.WorkflowProfileVersions.AnyAsync(v => v.ReasonForChange == reason));
        Assert.False(await verify.ElectronicSignatures.AnyAsync(s => s.UserId == user.Id && s.EntityType == ReviewEntityTypes.WorkflowProfile));
        Assert.False(await verify.AuditLogs.AnyAsync(a => a.ActionCode == WorkflowProfileService.VersionAddedActionCode && a.Reason == reason));
        // The refused attempt itself must stay on record (UnitOfWork re-writes it after the rollback).
        Assert.True(await verify.AuditLogs.CountAsync(a => a.Action == "SignatureFailed") > failedBefore);
    }

    [PostgresFact]
    public async Task AddVersion_CorrectPassword_PersistsEffectiveRowWithSignature_UnderTheTrigger()
    {
        await using var db = _fixture.CreateDbContext();
        var user = await SeedUserAsync(db);
        var reason = "marker-" + Guid.NewGuid();
        var service = TestServiceFactory.WorkflowProfile(db);

        var dto = await service.AddVersionAsync(
            new CreateWorkflowProfileVersionRequest(WorkflowType.WeightVariation, null, 1m, 5m, Tomorrow, reason, Password),
            user.Id, "127.0.0.1");

        await using var verify = _fixture.CreateDbContext();
        var row = await verify.WorkflowProfileVersions.SingleAsync(v => v.Id == dto.Id);
        Assert.Equal(WorkflowProfileStatus.Effective, row.Status);
        Assert.NotNull(row.ElectronicSignatureId);
        Assert.True(await verify.AuditLogs.AnyAsync(a => a.ActionCode == WorkflowProfileService.VersionAddedActionCode && a.Reason == reason));

        // A second version for the same scope must also succeed (the first one is now immutable).
        var second = await service.AddVersionAsync(
            new CreateWorkflowProfileVersionRequest(WorkflowType.WeightVariation, null, 2m, null, Tomorrow.AddDays(1), reason + "-2", Password),
            user.Id, "127.0.0.1");
        Assert.Equal(WorkflowProfileStatus.Effective, second.Status);
    }

    [PostgresFact]
    public async Task SignDraft_SecondSign_Rejected_AndStaleVersion_Conflicts()
    {
        int draftId;
        uint staleVersion;
        User user;
        await using (var seed = _fixture.CreateDbContext())
        {
            user = await SeedUserAsync(seed);
            var draft = new WorkflowProfileVersion
            {
                WorkflowType = WorkflowType.Disintegration, WorkUnits = 1m, EffectiveFrom = Tomorrow,
                Status = WorkflowProfileStatus.Draft, ReasonForChange = "Imported from legacy workload weight", CreatedByUserId = 0
            };
            seed.WorkflowProfileVersions.Add(draft);
            await seed.SaveChangesAsync();
            draftId = draft.Id;
            staleVersion = draft.Version;
        }

        // A colleague saves the draft first, so the form's version is stale.
        await using (var colleague = _fixture.CreateDbContext())
        {
            var row = await colleague.WorkflowProfileVersions.SingleAsync(v => v.Id == draftId);
            row.ReasonForChange = "edited by a colleague";
            await colleague.SaveChangesAsync();
        }

        var request = new SignWorkflowProfileDraftRequest(2m, 9m, Tomorrow, "Reviewed", Password);

        await using (var stale = _fixture.CreateDbContext())
        {
            stale.ExpectedVersion = staleVersion;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
                () => TestServiceFactory.WorkflowProfile(stale).SignDraftAsync(draftId, request, user.Id, null));
        }

        uint currentVersion;
        await using (var probe = _fixture.CreateDbContext())
            currentVersion = (await probe.WorkflowProfileVersions.AsNoTracking().SingleAsync(v => v.Id == draftId)).Version;

        await using (var current = _fixture.CreateDbContext())
        {
            current.ExpectedVersion = currentVersion;
            var signed = await TestServiceFactory.WorkflowProfile(current).SignDraftAsync(draftId, request, user.Id, null);
            Assert.Equal(WorkflowProfileStatus.Effective, signed.Status);
        }

        await using (var again = _fixture.CreateDbContext())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => TestServiceFactory.WorkflowProfile(again).SignDraftAsync(draftId, request, user.Id, null));
            Assert.Contains("already Effective", ex.Message);
        }

        await using var verify = _fixture.CreateDbContext();
        Assert.Equal(1, await verify.ElectronicSignatures.CountAsync(
            s => s.EntityType == ReviewEntityTypes.WorkflowProfile && s.EntityId == draftId));
    }
}
