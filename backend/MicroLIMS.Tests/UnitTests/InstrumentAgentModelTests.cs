using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Postgres-backed on purpose: unique indexes and the Equipment FK only
// exist in a real database.
[Collection("PostgresDatabaseCollection")]
public class InstrumentAgentModelTests
{
    private readonly PostgresTestFixture _fixture;

    public InstrumentAgentModelTests(PostgresTestFixture fixture) => _fixture = fixture;

    [PostgresFact]
    public async Task Code_and_KeyHash_are_unique_and_Equipment_is_required()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        await using var db = _fixture.CreateDbContext();
        var equipment = new Equipment
        {
            Name = $"HPLC {uid}", Code = $"HPLC-IA-{uid}", Type = EquipmentType.Hplc, SectionId = _fixture.SeededSectionId
        };
        db.Equipment.Add(equipment);
        await db.SaveChangesAsync();

        InstrumentAgent Agent(string code, string keyHash, int equipmentId) => new()
        {
            Code = code, Name = "Agent", EquipmentId = equipmentId, SectionId = _fixture.SeededSectionId,
            TimeZoneId = "Africa/Cairo", KeyHash = keyHash, KeyPrefix = keyHash[..4],
            IsActive = true, CreatedAt = DateTime.UtcNow, CreatedByUserId = _fixture.SeededUserId
        };

        var first = Agent($"AG-{uid}", $"{uid}a-hash", equipment.Id);
        db.InstrumentAgents.Add(first);
        await db.SaveChangesAsync();

        await using (var dupCode = _fixture.CreateDbContext())
        {
            dupCode.InstrumentAgents.Add(Agent(first.Code, $"{uid}b-hash", equipment.Id));
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => dupCode.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)ex.InnerException!).SqlState);
        }

        await using (var dupHash = _fixture.CreateDbContext())
        {
            dupHash.InstrumentAgents.Add(Agent($"AG2-{uid}", first.KeyHash, equipment.Id));
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => dupHash.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)ex.InnerException!).SqlState);
        }

        await using (var noEquipment = _fixture.CreateDbContext())
        {
            noEquipment.InstrumentAgents.Add(Agent($"AG3-{uid}", $"{uid}c-hash", equipmentId: 0));
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => noEquipment.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ((PostgresException)ex.InnerException!).SqlState);
        }

        await using (var noSection = _fixture.CreateDbContext())
        {
            var orphan = Agent($"AG4-{uid}", $"{uid}d-hash", equipment.Id);
            orphan.SectionId = 0;
            noSection.InstrumentAgents.Add(orphan);
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => noSection.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ((PostgresException)ex.InnerException!).SqlState);
        }
    }
}
