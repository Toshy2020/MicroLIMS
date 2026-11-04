using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.EquipmentPlan;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// The calibration-overdue job runs with no user. Against real PostgreSQL the
// status history's user column is a nullable FK to Users: the job must write
// NULL there (and on the equipment record), and the audit trail must name the
// System actor and the worker.
[Collection("PostgresDatabaseCollection")]
public class EquipmentOverduePostgresIntegrationTests
{
    private const string WorkerName = "EquipmentOverdueWorker";

    private readonly PostgresTestFixture _fixture;

    public EquipmentOverduePostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task OverdueJob_WritesNullUserIds_AndAuditRowsNameTheSystemActor()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        // A system administrator named "admin" exists: the job used to blame
        // exactly this kind of user. It must not be picked.
        await using (var seed = _fixture.CreateDbContext())
        {
            var adminRole = await seed.Roles.FirstOrDefaultAsync(r => r.Type == RoleType.SystemAdministrator);
            if (adminRole == null)
            {
                adminRole = new Role { Type = RoleType.SystemAdministrator, Name = "System Administrator", IsActive = true };
                seed.Roles.Add(adminRole);
                await seed.SaveChangesAsync();
            }
            seed.Users.Add(new User
            {
                FullName = "Overdue Probe Admin",
                Username = $"ovd_{suffix}".ToLowerInvariant(),
                PasswordHash = "dummy_hash",
                RoleId = adminRole.Id,
                IsActive = true
            });
            await seed.SaveChangesAsync();
        }

        // The job flips every in-service instrument due before "today", so the
        // test chooses a "today" in 2000 that no other test's data is older than.
        var today = new DateOnly(2000, 1, 2);
        var equipment = new EquipmentInventory
        {
            Code = $"OVD-{suffix}",
            InstrumentType = "Incubator",
            ManufacturerName = "Binder",
            Location = "Room 101",
            Status = EquipmentOperationalStatus.InService,
            CalibrationDueDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SectionId = _fixture.SeededSectionId,
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId
        };
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.EquipmentInventories.Add(equipment);
            await seed.SaveChangesAsync();
        }

        // The worker's context: no user, a process name.
        await using (var db = _fixture.CreateDbContext())
        {
            db.CurrentSystemProcessName = WorkerName;
            var clock = new LabClock(new FixedTimeProvider(new DateTimeOffset(2000, 1, 2, 8, 0, 0, TimeSpan.Zero)), TimeZoneInfo.Utc);
            var service = new EquipmentWorkService(db, clock, new ElectronicSignatureService(db), _fixture.CreateSequenceHelper(db));

            var changed = await service.ApplyOverdueAsync(today);

            Assert.True(changed >= 1);
        }

        await using var verify = _fixture.CreateDbContext();
        var reloaded = await verify.EquipmentInventories.AsNoTracking().SingleAsync(e => e.Id == equipment.Id);
        Assert.Equal(EquipmentOperationalStatus.OutOfService, reloaded.Status);
        Assert.Null(reloaded.LastModifiedByUserId);

        var history = await verify.EquipmentStatusHistories.AsNoTracking().SingleAsync(h => h.EquipmentInventoryId == equipment.Id);
        Assert.Null(history.ChangedByUserId);
        Assert.Equal("Calibration overdue since 2000-01-01 (automatic)", history.Comment);

        var inventoryAudit = await verify.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(EquipmentInventory) && a.EntityId == equipment.Id.ToString() && a.Action == "Update")
            .ToListAsync();
        var historyAudit = await verify.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(EquipmentStatusHistory) && a.EntityId == history.Id.ToString())
            .ToListAsync();
        var auditRows = new List<AuditLog> { Assert.Single(inventoryAudit), Assert.Single(historyAudit) };
        Assert.All(auditRows, row =>
        {
            Assert.Null(row.UserId);
            Assert.Equal(ActorType.System, row.ActorType);
            Assert.Equal(WorkerName, row.SystemProcessName);
        });
    }
}
