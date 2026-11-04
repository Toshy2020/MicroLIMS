using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// WP2.9 (decision D8): scripts/audit/append-negative-entityid-corrections.sql appends a correction
// row for every AuditLogs row with a negative EntityId - dry run writes nothing, apply appends,
// a second apply is a no-op, and the originals are never touched (the DB trigger would refuse).
[Collection("PostgresDatabaseCollection")]
public class NegativeEntityIdCorrectionScriptPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public NegativeEntityIdCorrectionScriptPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static string ScriptPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "scripts", "audit", "append-negative-entityid-corrections.sql");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("append-negative-entityid-corrections.sql not found above " + AppContext.BaseDirectory);
    }

    private (int ExitCode, string Output) RunPsql(bool apply)
    {
        var cs = new NpgsqlConnectionStringBuilder(_fixture.TestConnectionString);
        var psi = new ProcessStartInfo("psql")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("-h"); psi.ArgumentList.Add(cs.Host!);
        psi.ArgumentList.Add("-p"); psi.ArgumentList.Add(cs.Port.ToString());
        psi.ArgumentList.Add("-U"); psi.ArgumentList.Add(cs.Username!);
        psi.ArgumentList.Add("-d"); psi.ArgumentList.Add(cs.Database!);
        psi.ArgumentList.Add("-v"); psi.ArgumentList.Add("ON_ERROR_STOP=1");
        if (apply) { psi.ArgumentList.Add("-v"); psi.ArgumentList.Add("apply=1"); }
        psi.ArgumentList.Add("-f"); psi.ArgumentList.Add(ScriptPath());
        if (!string.IsNullOrEmpty(cs.Password)) psi.Environment["PGPASSWORD"] = cs.Password;

        using var p = Process.Start(psi)!;
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, stdout + stderr);
    }

    [PostgresFact]
    public async Task DryRunWritesNothing_ApplyAppendsCorrections_SecondApplyIsANoOp_OriginalsUntouched()
    {
        await using var db = _fixture.CreateDbContext();
        var entity = "ProbeEntity" + Guid.NewGuid().ToString("N")[..8];
        var realId = 910_000 + Random.Shared.Next(1, 8_000);

        // Created with temporary keys (the old bug): its own Id and the FK to another new row are negative.
        var tempIdA = -2_147_480_001 - Random.Shared.Next(0, 1000);
        var tempFk = -2_147_480_900 - Random.Shared.Next(0, 1000);
        var tempIdB = -2_147_479_001 - Random.Shared.Next(0, 1000);
        var created = new AuditLog
        {
            EntityName = entity, EntityId = tempIdA.ToString(), Action = "Create", UserId = 1, Timestamp = DateTime.UtcNow,
            NewValue = JsonSerializer.Serialize(new Dictionary<string, object> { ["Id"] = tempIdA, ["ParentId"] = tempFk, ["Name"] = "probe" })
        };
        // Its first later Update, written after the fix, names the real id and carries the real FK.
        var updated = new AuditLog
        {
            EntityName = entity, EntityId = realId.ToString(), Action = "Update", UserId = 1, Timestamp = DateTime.UtcNow,
            PreviousValue = JsonSerializer.Serialize(new Dictionary<string, object> { ["Id"] = realId, ["ParentId"] = 4242, ["Name"] = "probe" }),
            NewValue = JsonSerializer.Serialize(new Dictionary<string, object> { ["Id"] = realId, ["ParentId"] = 4242, ["Name"] = "probe2" })
        };
        // A negative row nothing can resolve (never updated).
        var orphan = new AuditLog
        {
            EntityName = entity + "X", EntityId = tempIdB.ToString(), Action = "Create", UserId = 1, Timestamp = DateTime.UtcNow,
            NewValue = JsonSerializer.Serialize(new Dictionary<string, object> { ["Id"] = tempIdB, ["Name"] = "lonely" })
        };
        db.AuditLogs.AddRange(created, updated, orphan);
        await db.SaveChangesAsync();
        // Order matters for the "later Update" rule: the Update row got the higher Id.
        Assert.True(updated.Id > created.Id);

        Task<List<AuditLog>> Corrections() => db.AuditLogs.AsNoTracking()
            .Where(a => a.ActionCode == "AuditEntityIdCorrection" && (a.EntityId == created.Id.ToString() || a.EntityId == orphan.Id.ToString()))
            .OrderBy(a => a.Id).ToListAsync();

        // Dry run: reports both rows, writes nothing.
        var dry = RunPsql(apply: false);
        Assert.True(dry.ExitCode == 0, dry.Output);
        Assert.Contains("DRY RUN", dry.Output);
        Assert.Contains(tempIdA.ToString(), dry.Output);
        Assert.Contains(realId.ToString(), dry.Output);
        Assert.Contains(tempIdB.ToString(), dry.Output);
        Assert.Empty(await Corrections());

        // Apply: two correction rows, system actor, pointing at the originals.
        var applied = RunPsql(apply: true);
        Assert.True(applied.ExitCode == 0, applied.Output);
        var rows = await Corrections();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.Equal("AuditLog", r.EntityName);
            Assert.Equal(Domain.Enums.ActorType.System, r.ActorType);
            Assert.Equal(Domain.Enums.AuditActionCategory.Other, r.ActionCategory);
            Assert.Null(r.UserId);
        });
        var forCreated = JsonDocument.Parse(rows.Single(r => r.EntityId == created.Id.ToString()).NewValue!).RootElement;
        Assert.Equal(created.Id, forCreated.GetProperty("originalAuditLogId").GetInt32());
        Assert.Equal(entity, forCreated.GetProperty("originalEntityName").GetString());
        Assert.Equal(tempIdA.ToString(), forCreated.GetProperty("originalEntityId").GetString());
        Assert.Equal(realId.ToString(), forCreated.GetProperty("resolvedEntityId").GetString());
        var forOrphan = JsonDocument.Parse(rows.Single(r => r.EntityId == orphan.Id.ToString()).NewValue!).RootElement;
        Assert.Equal(JsonValueKind.Null, forOrphan.GetProperty("resolvedEntityId").ValueKind);
        Assert.Equal("unresolved", forOrphan.GetProperty("resolution").GetString());

        // Originals are exactly as written.
        var original = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.Id == created.Id);
        Assert.Equal(tempIdA.ToString(), original.EntityId);
        Assert.Equal(created.NewValue, original.NewValue);

        // Idempotent: nothing left to correct for these rows.
        var again = RunPsql(apply: true);
        Assert.True(again.ExitCode == 0, again.Output);
        Assert.Equal(2, (await Corrections()).Count);

        // And the database still refuses to edit the audit trail.
        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlRawAsync($"UPDATE \"AuditLogs\" SET \"EntityId\" = '1' WHERE \"Id\" = {created.Id}"));
    }
}
