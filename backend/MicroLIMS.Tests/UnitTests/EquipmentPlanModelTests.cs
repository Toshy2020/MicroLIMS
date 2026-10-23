using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

[Collection("PostgresDatabaseCollection")]
public class EquipmentPlanModelTests
{
    private readonly PostgresTestFixture _fixture;

    public EquipmentPlanModelTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void SignatureMeaning_new_values_are_appended()
    {
        Assert.Equal((int)SignatureMeaning.MonitoringDeviationClosed + 1, (int)SignatureMeaning.EquipmentPlanApproved);
        Assert.Equal((int)SignatureMeaning.EquipmentPlanApproved + 1, (int)SignatureMeaning.CalibrationRecorded);
        Assert.Equal((int)SignatureMeaning.CalibrationRecorded + 1, (int)SignatureMeaning.MaintenanceEntryRecorded);
        Assert.Equal((int)SignatureMeaning.MaintenanceEntryRecorded + 1, (int)SignatureMeaning.MaintenanceEntryVoided);
    }

    [PostgresFact]
    public async Task EquipmentTask_unique_per_instrument_type_year_sequence()
    {
        await using var db = _fixture.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();

        var equipment = new EquipmentInventory
        {
            Code = $"EQ-TEST-{Guid.NewGuid():N}"[..20],
            InstrumentType = "Balance",
            Location = "Lab 1",
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId
        };
        db.EquipmentInventories.Add(equipment);
        await db.SaveChangesAsync();

        var task1 = new EquipmentTask
        {
            EquipmentInventoryId = equipment.Id,
            TaskType = EquipmentTaskType.Calibration,
            Year = 2026,
            Sequence = 1,
            OriginalDueDate = new DateOnly(2026, 4, 1),
            DueDate = new DateOnly(2026, 4, 1),
            IntervalSnapshot = "Every 6 months"
        };
        db.EquipmentTasks.Add(task1);
        await db.SaveChangesAsync();

        var task2 = new EquipmentTask
        {
            EquipmentInventoryId = equipment.Id,
            TaskType = EquipmentTaskType.Calibration,
            Year = 2026,
            Sequence = 1,
            OriginalDueDate = new DateOnly(2026, 4, 1),
            DueDate = new DateOnly(2026, 4, 1),
            IntervalSnapshot = "Every 6 months"
        };
        db.EquipmentTasks.Add(task2);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        await tx.RollbackAsync();
    }
}
