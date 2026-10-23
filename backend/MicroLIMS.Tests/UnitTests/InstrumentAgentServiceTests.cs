using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class InstrumentAgentServiceTests
{
    private sealed class SpyAudit : IAuditEventService
    {
        public List<(string Code, AuditActionCategory Category, string RecordType, string? EntityId, string? Reason)> Events { get; } = new();

        public Task<AuditLog> RecordUserEventAsync(string actionCode, AuditActionCategory actionCategory, string recordType,
            int? documentMasterId = null, int? documentRevisionId = null, string? reason = null, Guid? correlationId = null,
            string? sourceContext = null, IEnumerable<AuditFieldChange>? changes = null, string? entityId = null,
            CancellationToken cancellationToken = default, int? sampleId = null)
        {
            Events.Add((actionCode, actionCategory, recordType, entityId, reason));
            return Task.FromResult(new AuditLog());
        }

        public Task<AuditLog> RecordSystemEventAsync(string systemProcessName, string actionCode, AuditActionCategory actionCategory,
            string recordType, int? documentMasterId = null, int? documentRevisionId = null, string? reason = null,
            Guid? correlationId = null, string? sourceContext = null, IEnumerable<AuditFieldChange>? changes = null,
            string? entityId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static string Sha256Hex(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

    private sealed record World(MicroLimsDbContext Db, InstrumentAgentService Service, SpyAudit Audit,
        Equipment Hplc, Equipment Incubator, Equipment OtherSectionHplc, User Admin, User SectionHead);

    private static World NewWorld()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var other = new DocumentSection { Name = "Chemistry", Code = "CHEM", DepartmentId = micro.DepartmentId, IsActive = true };
        db.DocumentSections.Add(other);
        var adminRole = new Role { Name = "Admin", Type = RoleType.SystemAdministrator, IsActive = true };
        var headRole = new Role { Name = "Head", Type = RoleType.SectionHead, IsActive = true };
        db.Roles.AddRange(adminRole, headRole);
        db.SaveChanges();
        var admin = new User { Username = "adm", FullName = "Admin", RoleId = adminRole.Id, Role = adminRole, IsActive = true };
        var head = new User { Username = "head", FullName = "Head", RoleId = headRole.Id, Role = headRole, IsActive = true };
        db.Users.AddRange(admin, head);
        db.SaveChanges();
        TestServiceFactory.AssignUserToMicroSection(db, head.Id);

        var hplc = new Equipment { Name = "HPLC 1", Code = "HPLC-01", Type = EquipmentType.Hplc, SectionId = micro.Id };
        var inc = new Equipment { Name = "Incubator", Code = "INC-01", Type = EquipmentType.Incubator, SectionId = micro.Id };
        var otherHplc = new Equipment { Name = "HPLC 2", Code = "HPLC-02", Type = EquipmentType.Hplc, SectionId = other.Id };
        db.Equipment.AddRange(hplc, inc, otherHplc);
        db.SaveChanges();

        var audit = new SpyAudit();
        return new World(db, new InstrumentAgentService(db, new UserSectionScopeService(db), audit), audit, hplc, inc, otherHplc, admin, head);
    }

    [Fact]
    public async Task Register_returns_plain_key_once_and_stores_only_its_hash()
    {
        var w = NewWorld();
        var (agent, key) = await w.Service.RegisterAsync(w.Hplc.Id, "Lab PC", "Africa/Cairo", w.Admin.Id, default);

        Assert.Equal("AGT-HPLC-01", agent.Code);
        Assert.StartsWith("lims_agent_AGT-HPLC-01_", key);
        Assert.Equal(key["lims_agent_AGT-HPLC-01_".Length..][..8], agent.KeyPrefix);
        Assert.Equal(w.Hplc.SectionId, agent.SectionId);

        var stored = await w.Db.InstrumentAgents.SingleAsync();
        Assert.Equal(Sha256Hex(key), stored.KeyHash);
        Assert.DoesNotContain(key, stored.KeyHash);
        Assert.True(stored.IsActive);
        Assert.Equal(w.Admin.Id, stored.CreatedByUserId);
        // 32 random bytes, base64url, no padding = 43 chars after the code.
        Assert.Equal(43, key["lims_agent_AGT-HPLC-01_".Length..].Length);

        var ev = Assert.Single(w.Audit.Events);
        Assert.Equal(("INSTRUMENT_AGENT_REGISTERED", AuditActionCategory.Security, "InstrumentAgent"), (ev.Code, ev.Category, ev.RecordType));
        Assert.Equal(agent.Id.ToString(), ev.EntityId);
    }

    [Fact]
    public void Dto_exposes_neither_key_nor_hash()
    {
        var names = typeof(MicroLIMS.Application.DTOs.InstrumentAgentDto).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("KeyHash", names);
        Assert.DoesNotContain("PlainKey", names);
        Assert.Contains("KeyPrefix", names);
    }

    [Fact]
    public async Task Register_rejects_non_hplc_equipment_missing_equipment_and_bad_timezone()
    {
        var w = NewWorld();
        await Assert.ThrowsAsync<InvalidOperationException>(() => w.Service.RegisterAsync(w.Incubator.Id, "x", "Africa/Cairo", w.Admin.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => w.Service.RegisterAsync(9999, "x", "Africa/Cairo", w.Admin.Id, default));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => w.Service.RegisterAsync(w.Hplc.Id, "x", "Mars/Olympus", w.Admin.Id, default));
        Assert.Contains("time zone", ex.Message, StringComparison.OrdinalIgnoreCase);
        await Assert.ThrowsAsync<InvalidOperationException>(() => w.Service.RegisterAsync(w.Hplc.Id, "  ", "Africa/Cairo", w.Admin.Id, default));
        Assert.Empty(w.Db.InstrumentAgents);
        Assert.Empty(w.Audit.Events);
    }

    [Fact]
    public async Task Register_requires_access_to_the_equipment_section()
    {
        var w = NewWorld();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => w.Service.RegisterAsync(w.OtherSectionHplc.Id, "x", "Africa/Cairo", w.SectionHead.Id, default));
        var (agent, _) = await w.Service.RegisterAsync(w.Hplc.Id, "x", "Africa/Cairo", w.SectionHead.Id, default);
        Assert.Equal(w.Hplc.SectionId, agent.SectionId);
    }

    [Fact]
    public async Task Register_twice_for_one_equipment_is_rejected_until_the_first_is_revoked()
    {
        var w = NewWorld();
        var (first, _) = await w.Service.RegisterAsync(w.Hplc.Id, "a", "Africa/Cairo", w.Admin.Id, default);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => w.Service.RegisterAsync(w.Hplc.Id, "b", "Africa/Cairo", w.Admin.Id, default));
        Assert.Contains(first.Code, ex.Message);

        await w.Service.RevokeAsync(first.Id, "decommissioned", w.Admin.Id, default);
        var (second, _) = await w.Service.RegisterAsync(w.Hplc.Id, "b", "Africa/Cairo", w.Admin.Id, default);
        Assert.NotEqual(first.Code, second.Code);
    }

    [Fact]
    public async Task Rotate_replaces_the_hash_and_the_old_key_no_longer_matches()
    {
        var w = NewWorld();
        var (agent, oldKey) = await w.Service.RegisterAsync(w.Hplc.Id, "a", "Africa/Cairo", w.Admin.Id, default);
        var (rotated, newKey) = await w.Service.RotateKeyAsync(agent.Id, w.Admin.Id, default);

        Assert.NotEqual(oldKey, newKey);
        Assert.Equal(newKey["lims_agent_AGT-HPLC-01_".Length..][..8], rotated.KeyPrefix);
        Assert.NotEqual(agent.KeyPrefix, rotated.KeyPrefix);
        Assert.False(await w.Db.InstrumentAgents.AnyAsync(a => a.KeyHash == Sha256Hex(oldKey)));
        Assert.True(await w.Db.InstrumentAgents.AnyAsync(a => a.KeyHash == Sha256Hex(newKey)));
        Assert.Equal("INSTRUMENT_AGENT_KEY_ROTATED", w.Audit.Events[^1].Code);
    }

    [Fact]
    public async Task Revoke_requires_a_reason_deactivates_and_audits()
    {
        var w = NewWorld();
        var (agent, key) = await w.Service.RegisterAsync(w.Hplc.Id, "a", "Africa/Cairo", w.Admin.Id, default);

        await Assert.ThrowsAsync<InvalidOperationException>(() => w.Service.RevokeAsync(agent.Id, "  ", w.Admin.Id, default));
        Assert.True((await w.Db.InstrumentAgents.SingleAsync()).IsActive);

        await w.Service.RevokeAsync(agent.Id, "PC stolen", w.Admin.Id, default);
        var stored = await w.Db.InstrumentAgents.SingleAsync();
        Assert.False(stored.IsActive);
        Assert.NotNull(stored.RevokedAt);
        Assert.Equal(w.Admin.Id, stored.RevokedByUserId);
        Assert.Equal("PC stolen", stored.RevokeReason);
        Assert.Equal(Sha256Hex(key), stored.KeyHash); // row kept: deactivate, never delete

        var ev = w.Audit.Events[^1];
        Assert.Equal(("INSTRUMENT_AGENT_REVOKED", AuditActionCategory.Security, "PC stolen"), (ev.Code, ev.Category, ev.Reason));

        // A revoked agent can neither rotate nor be revoked again.
        await Assert.ThrowsAsync<InvalidOperationException>(() => w.Service.RotateKeyAsync(agent.Id, w.Admin.Id, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => w.Service.RevokeAsync(agent.Id, "again", w.Admin.Id, default));
    }

    [Fact]
    public async Task Rotate_and_revoke_of_an_unknown_agent_are_not_found()
    {
        var w = NewWorld();
        await Assert.ThrowsAsync<NotFoundException>(() => w.Service.RotateKeyAsync(42, w.Admin.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => w.Service.RevokeAsync(42, "x", w.Admin.Id, default));
    }

    [Fact]
    public async Task Rotate_and_revoke_require_access_to_the_agents_section()
    {
        var w = NewWorld();
        var (agent, _) = await w.Service.RegisterAsync(w.OtherSectionHplc.Id, "a", "Africa/Cairo", w.Admin.Id, default);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => w.Service.RotateKeyAsync(agent.Id, w.SectionHead.Id, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => w.Service.RevokeAsync(agent.Id, "x", w.SectionHead.Id, default));
    }
}

[Collection("PostgresDatabaseCollection")]
public class InstrumentAgentServicePostgresTests
{
    private readonly PostgresTestFixture _fixture;
    public InstrumentAgentServicePostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    [PostgresFact]
    public async Task Register_lookup_by_hash_then_revoke_on_real_postgres()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        await using var db = _fixture.CreateDbContext();
        var equipment = new Equipment { Name = $"HPLC {uid}", Code = $"HPLC-IS-{uid}", Type = EquipmentType.Hplc, SectionId = _fixture.SeededSectionId };
        db.Equipment.Add(equipment);
        await db.SaveChangesAsync();

        var service = new InstrumentAgentService(db, new UserSectionScopeService(db),
            new AuditEventService(db, new DatabaseSequenceHelper(db)));

        var (agent, key) = await service.RegisterAsync(equipment.Id, "Lab PC", "Africa/Cairo", _fixture.SeededUserId, default);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

        await using (var reader = _fixture.CreateDbContext())
        {
            var found = await reader.InstrumentAgents.SingleAsync(a => a.KeyHash == hash);
            Assert.Equal(agent.Id, found.Id);
            Assert.True(found.IsActive);
            Assert.Equal(equipment.SectionId, found.SectionId);
        }
        await using (var actor = _fixture.CreateDbContext())
        {
            var reg = await actor.AuditLogs.SingleAsync(l => l.ActionCode == "INSTRUMENT_AGENT_REGISTERED" && l.EntityId == agent.Id.ToString());
            Assert.Equal(_fixture.SeededUserId, reg.UserId);
        }

        await service.RevokeAsync(agent.Id, "test", _fixture.SeededUserId, default);

        await using var after = _fixture.CreateDbContext();
        Assert.False((await after.InstrumentAgents.SingleAsync(a => a.KeyHash == hash)).IsActive);
        var events = await after.AuditLogs.Where(l => l.ActionCategory == AuditActionCategory.Security && l.EntityId == agent.Id.ToString()).Select(l => l.ActionCode).ToListAsync();
        Assert.Equal(new[] { "INSTRUMENT_AGENT_REGISTERED", "INSTRUMENT_AGENT_REVOKED" }, events.OrderBy(c => c == "INSTRUMENT_AGENT_REVOKED").ToArray());
    }

    private sealed class ThrowingAudit : IAuditEventService
    {
        public Task<AuditLog> RecordUserEventAsync(string actionCode, AuditActionCategory actionCategory, string recordType,
            int? documentMasterId = null, int? documentRevisionId = null, string? reason = null, Guid? correlationId = null,
            string? sourceContext = null, IEnumerable<AuditFieldChange>? changes = null, string? entityId = null,
            CancellationToken cancellationToken = default, int? sampleId = null) => throw new InvalidOperationException("audit down");

        public Task<AuditLog> RecordSystemEventAsync(string systemProcessName, string actionCode, AuditActionCategory actionCategory,
            string recordType, int? documentMasterId = null, int? documentRevisionId = null, string? reason = null,
            Guid? correlationId = null, string? sourceContext = null, IEnumerable<AuditFieldChange>? changes = null,
            string? entityId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [PostgresFact]
    public async Task A_failed_audit_write_rolls_the_register_back()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        await using var db = _fixture.CreateDbContext();
        var equipment = new Equipment { Name = $"HPLC {uid}", Code = $"HPLC-RB-{uid}", Type = EquipmentType.Hplc, SectionId = _fixture.SeededSectionId };
        db.Equipment.Add(equipment);
        await db.SaveChangesAsync();

        var service = new InstrumentAgentService(db, new UserSectionScopeService(db), new ThrowingAudit());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(equipment.Id, "Lab PC", "Africa/Cairo", _fixture.SeededUserId, default));

        await using var after = _fixture.CreateDbContext();
        Assert.False(await after.InstrumentAgents.AnyAsync(a => a.EquipmentId == equipment.Id));
    }
}
