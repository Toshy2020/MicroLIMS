using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests.Kpi;

// Model-level checks that run without Postgres. The check constraints themselves are exercised in KpiSettingsMigrationPostgresTests.
public class KpiSettingsModelTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public void KpiSetting_HasUniqueSectionIndex_AndHoursPrecision()
    {
        using var db = NewDb();
        var et = db.Model.FindEntityType(typeof(KpiSetting))!;
        Assert.Contains(et.GetIndexes(), i => i.IsUnique && i.Properties.Single().Name == nameof(KpiSetting.SectionId));
        var hours = et.FindProperty(nameof(KpiSetting.HoursPerAnalystDay))!;
        Assert.Equal(4, hours.GetPrecision());
        Assert.Equal(2, hours.GetScale());
    }

    [Fact]
    public void AnalystAvailability_ReasonIsRequiredAndBounded_FractionIsNumeric32()
    {
        using var db = NewDb();
        var et = db.Model.FindEntityType(typeof(AnalystAvailability))!;
        var reason = et.FindProperty(nameof(AnalystAvailability.Reason))!;
        Assert.False(reason.IsNullable);
        Assert.Equal(500, reason.GetMaxLength());
        var fraction = et.FindProperty(nameof(AnalystAvailability.FractionAvailable))!;
        Assert.Equal(3, fraction.GetPrecision());
        Assert.Equal(2, fraction.GetScale());
    }

    [Fact]
    public void KpiSetting_EntityDefaults_AgreeWithKpiSettingDefaults()
    {
        var s = new KpiSetting();
        Assert.Equal(KpiSettingDefaults.HoursPerAnalystDay, s.HoursPerAnalystDay);
        Assert.Equal(KpiSettingDefaults.WorkingWeekdays, s.WorkingWeekdays);
        Assert.Equal(KpiSettingDefaults.LoadHorizonDays, s.LoadHorizonDays);
    }
}
