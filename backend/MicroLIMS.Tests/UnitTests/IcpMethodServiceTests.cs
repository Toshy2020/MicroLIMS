using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class IcpMethodServiceTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);
        db.CurrentUserId = 1;
        return db;
    }

    private static async Task<(DocumentSection section, int userId)> SeedAsync(MicroLimsDbContext db)
    {
        var section = TestServiceFactory.EnsureMicroSection(db);
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { Username = "u_" + Guid.NewGuid().ToString("N")[..6], FullName = "Test User", RoleId = role.Id, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        TestServiceFactory.AssignUserToMicroSection(db, user.Id);
        return (section, user.Id);
    }

    private static async Task<MaterialMasterEntry> AddEntryAsync(
        MicroLimsDbContext db, int sectionId, string code, bool isActive = true,
        MaterialMasterCategory category = MaterialMasterCategory.ReferenceStandard)
    {
        var entry = new MaterialMasterEntry
        {
            SectionId = sectionId,
            Code = code,
            Name = code,
            Category = category,
            BaseUnit = MaterialUnit.Gram,
            IsActive = isActive,
            CreatedByUserId = 1,
            CreatedAt = DateTime.UtcNow,
            LastModifiedByUserId = 1,
            LastModifiedAt = DateTime.UtcNow
        };
        db.MaterialMasterEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    private static SaveIcpMethodRequest Req(int calStdId, params IcpElementInput[] elements) => new(
        Name: "Minerals by ICP-OES", Abbreviation: "min-icp", EffectiveDate: new DateTime(2026, 10, 1),
        Mode: IcpMethodMode.MineralAssay, StandardLevelsMgPerL: "6, 0.1, 1, 0.5, 3",
        CalibrationStandardEntryId: calStdId, MinCorrelation: 0.999m, SampleVolumeMl: 50m,
        Elements: elements.Length > 0 ? elements.ToList() : new() { new IcpElementInput(null, "zn", 213.857m, AnalyteView.Axial) });

    [Fact]
    public async Task Create_ValidMineralMethod_NormalisesAndAudits()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var std = await AddEntryAsync(db, section.Id, "RS-ICP-1");
        var svc = TestServiceFactory.IcpMethod(db);

        var m = await svc.CreateAsync(Req(std.Id), userId);

        Assert.Equal("MIN-ICP", m.Abbreviation);
        Assert.Equal("0.1, 0.5, 1, 3, 6", m.StandardLevelsMgPerL);
        Assert.Equal("Zn", m.Elements.Single().Symbol);
        Assert.Equal(1m, m.Elements.Single().ConversionFactor);
        Assert.Equal(24, m.MaxCalibrationAgeHours);
        Assert.Single(db.AuditLogs.Where(l => l.ActionCode == "IcpMethod.Created"));
    }

    [Fact]
    public async Task Create_IcvSameAsCalibrationStandard_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var std = await AddEntryAsync(db, section.Id, "RS-ICP-1");
        var r = Req(std.Id) with { RequireIcv = true, IcvStandardEntryId = std.Id, IcvNominalMgPerL = 1m, IcvRecoveryLowPercent = 90m, IcvRecoveryHighPercent = 110m };
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => TestServiceFactory.IcpMethod(db).CreateAsync(r, userId));
        Assert.Equal("The ICV standard must be a second source, not the calibration standard.", ex.Message);
    }

    [Fact]
    public async Task Create_CheckOffWithValues_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var std = await AddEntryAsync(db, section.Id, "RS-ICP-1");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestServiceFactory.IcpMethod(db).CreateAsync(Req(std.Id) with { CcvNominalMgPerL = 5m }, userId));
        Assert.Equal("Blank, ICV and CCV values are only used when that check is on.", ex.Message);
    }

    [Fact]
    public async Task Update_ChangingMode_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var std = await AddEntryAsync(db, section.Id, "RS-ICP-1");
        var svc = TestServiceFactory.IcpMethod(db);
        var m = await svc.CreateAsync(Req(std.Id), userId);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UpdateAsync(m.Id, Req(std.Id) with { Mode = IcpMethodMode.ElementalImpurities, Reason = "x" }, userId));
        Assert.Equal("The mode can't be changed after the method is created.", ex.Message);
    }

    [Fact]
    public async Task Update_KeepsElementIds_AndRemovingElementUsedBySpec_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var std = await AddEntryAsync(db, section.Id, "RS-ICP-1");
        var svc = TestServiceFactory.IcpMethod(db);
        var m = await svc.CreateAsync(Req(std.Id, new IcpElementInput(null, "Zn", 213.857m, AnalyteView.Axial), new IcpElementInput(null, "Ca", 317.933m, AnalyteView.Radial)), userId);
        var zn = m.Elements.Single(e => e.Symbol == "Zn");
        var ca = m.Elements.Single(e => e.Symbol == "Ca");

        // keep both, change Zn wavelength -> same ids
        var kept = await svc.UpdateAsync(m.Id, Req(std.Id, new IcpElementInput(zn.Id, "Zn", 206.200m, AnalyteView.Axial), new IcpElementInput(ca.Id, "Ca", 317.933m, AnalyteView.Radial)) with { Reason = "wl" }, userId);
        Assert.Equal(zn.Id, kept.Elements.Single(e => e.Symbol == "Zn").Id);

        db.Specifications.Add(new Specification { ItemId = 1, TestCode = "ICP-T", ParameterName = "Ca", IcpMethodElementId = ca.Id, LimitType = LimitType.Range, LowerLimit = 90, UpperLimit = 110 });
        await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UpdateAsync(m.Id, Req(std.Id, new IcpElementInput(zn.Id, "Zn", 206.200m, AnalyteView.Axial)) with { Reason = "drop Ca" }, userId));
        Assert.Equal("Element \"Ca\" is used by specifications; it can't be removed.", ex.Message);
    }

    public static IEnumerable<object[]> InvalidRequests() => new[]
    {
        Case("name blank", r => r with { Name = " " }, "Method name is required."),
        Case("name too long", r => r with { Name = new string('x', 201) }, "Method name cannot exceed 200 characters."),
        Case("abbreviation blank", r => r with { Abbreviation = "" }, "Abbreviation is required."),
        Case("abbreviation bad chars", r => r with { Abbreviation = "a b" }, "Abbreviation must be 2-20 uppercase letters, digits or hyphens."),
        Case("single level", r => r with { StandardLevelsMgPerL = "1" }, "At least two standard levels are required."),
        Case("correlation above 1", r => r with { MinCorrelation = 1.2m }, "Minimum correlation must be greater than 0 and at most 1."),
        Case("sample volume zero", r => r with { SampleVolumeMl = 0m }, "Sample volume must be greater than zero."),
        Case("dilution below 1", r => r with { DilutionFactor = 0.5m }, "Dilution factor must be 1 or more."),
        Case("calibration age zero", r => r with { MaxCalibrationAgeHours = 0 }, "Calibration age must be between 1 and 168 hours."),
        Case("calibration age above 168", r => r with { MaxCalibrationAgeHours = 169 }, "Calibration age must be between 1 and 168 hours."),
        Case("blank on without limit", r => r with { RequireBlank = true }, "Blank limit (mg/L) is required when the blank check is on."),
        Case("icv on missing nominal", r => r with { RequireIcv = true, IcvStandardEntryId = 999, IcvRecoveryLowPercent = 90m, IcvRecoveryHighPercent = 110m },
            "ICV standard, nominal (mg/L) and recovery limits are required when the ICV check is on."),
        Case("ccv low not below high", r => r with { RequireCcv = true, CcvNominalMgPerL = 1m, CcvRecoveryLowPercent = 110m, CcvRecoveryHighPercent = 90m },
            "CCV nominal (mg/L) and recovery limits are required when the CCV check is on."),
        Case("blank value with check off", r => r with { BlankMaxMgPerL = 0.1m }, "Blank, ICV and CCV values are only used when that check is on."),
        Case("no elements", r => r with { Elements = new() }, "At least one element is required."),
        Case("symbol too long", r => r with { Elements = new() { new IcpElementInput(null, "Abcd", 200m, AnalyteView.Axial) } }, "Element symbol must be 1-3 letters."),
        Case("symbol blank", r => r with { Elements = new() { new IcpElementInput(null, " ", 200m, AnalyteView.Axial) } }, "Element symbol must be 1-3 letters."),
        Case("duplicate symbol", r => r with { Elements = new() { new IcpElementInput(null, "zn", 200m, AnalyteView.Axial), new IcpElementInput(null, "Zn", 201m, AnalyteView.Radial) } },
            "Element \"Zn\" is listed more than once."),
        Case("wavelength zero", r => r with { Elements = new() { new IcpElementInput(null, "Zn", 0m, AnalyteView.Axial) } }, "Zn: wavelength must be greater than zero."),
        Case("conversion factor zero", r => r with { Elements = new() { new IcpElementInput(null, "Zn", 200m, AnalyteView.Axial, 0m) } }, "Zn: conversion factor must be greater than zero."),
    };

    private static object[] Case(string name, Func<SaveIcpMethodRequest, SaveIcpMethodRequest> mutate, string message) => new object[] { name, mutate, message };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Create_InvalidRequest_ThrowsExactMessage(string caseName, Func<SaveIcpMethodRequest, SaveIcpMethodRequest> mutate, string expected)
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var std = await AddEntryAsync(db, section.Id, "RS-ICP-1");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestServiceFactory.IcpMethod(db).CreateAsync(mutate(Req(std.Id)), userId));

        Assert.True(expected == ex.Message, $"{caseName}: expected \"{expected}\" but got \"{ex.Message}\"");
    }

    [Fact]
    public async Task Create_CalibrationStandardMissing_Throws()
    {
        await using var db = NewDb();
        var (_, userId) = await SeedAsync(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => TestServiceFactory.IcpMethod(db).CreateAsync(Req(9999), userId));
        Assert.Equal("Calibration standard not found.", ex.Message);
    }

    [Fact]
    public async Task Create_CalibrationStandardNotReferenceStandard_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var reagent = await AddEntryAsync(db, section.Id, "RG-1", category: MaterialMasterCategory.Reagent);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => TestServiceFactory.IcpMethod(db).CreateAsync(Req(reagent.Id), userId));
        Assert.Equal("Calibration standard \"RG-1\" must be a reference standard.", ex.Message);
    }

    [Fact]
    public async Task Create_CalibrationStandardInactive_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var std = await AddEntryAsync(db, section.Id, "RS-OLD", isActive: false);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => TestServiceFactory.IcpMethod(db).CreateAsync(Req(std.Id), userId));
        Assert.Equal("Calibration standard \"RS-OLD\" is inactive.", ex.Message);
    }

    [Fact]
    public async Task Create_IcvStandardInactive_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var cal = await AddEntryAsync(db, section.Id, "RS-ICP-1");
        var icv = await AddEntryAsync(db, section.Id, "RS-ICV", isActive: false);
        var r = Req(cal.Id) with { RequireIcv = true, IcvStandardEntryId = icv.Id, IcvNominalMgPerL = 1m, IcvRecoveryLowPercent = 90m, IcvRecoveryHighPercent = 110m };
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => TestServiceFactory.IcpMethod(db).CreateAsync(r, userId));
        Assert.Equal("ICV standard \"RS-ICV\" is inactive.", ex.Message);
    }

    [Fact]
    public async Task Create_DuplicateAbbreviation_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var std = await AddEntryAsync(db, section.Id, "RS-ICP-1");
        var svc = TestServiceFactory.IcpMethod(db);
        await svc.CreateAsync(Req(std.Id), userId);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CreateAsync(Req(std.Id), userId));
        Assert.Equal("An ICP method abbreviated \"MIN-ICP\" already exists.", ex.Message);
    }

    [Fact]
    public async Task Update_WithoutReason_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var std = await AddEntryAsync(db, section.Id, "RS-ICP-1");
        var svc = TestServiceFactory.IcpMethod(db);
        var m = await svc.CreateAsync(Req(std.Id), userId);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.UpdateAsync(m.Id, Req(std.Id), userId));
        Assert.Equal("A reason is required.", ex.Message);
    }

    [Fact]
    public async Task Update_ElementIdFromAnotherMethod_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var std = await AddEntryAsync(db, section.Id, "RS-ICP-1");
        var svc = TestServiceFactory.IcpMethod(db);
        var m = await svc.CreateAsync(Req(std.Id), userId);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UpdateAsync(m.Id, Req(std.Id, new IcpElementInput(424242, "Zn", 213.857m, AnalyteView.Axial)) with { Reason = "x" }, userId));
        Assert.Equal("Element 424242 does not belong to this method.", ex.Message);
    }

    [Fact]
    public async Task Update_InactiveStandardAlreadyOnMethod_IsAllowed()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var std = await AddEntryAsync(db, section.Id, "RS-ICP-1");
        var svc = TestServiceFactory.IcpMethod(db);
        var m = await svc.CreateAsync(Req(std.Id, new IcpElementInput(null, "Zn", 213.857m, AnalyteView.Axial)), userId);
        (await db.MaterialMasterEntries.FirstAsync(e => e.Id == std.Id)).IsActive = false;
        await db.SaveChangesAsync();

        var updated = await svc.UpdateAsync(m.Id, Req(std.Id, new IcpElementInput(m.Elements[0].Id, "Zn", 213.857m, AnalyteView.Axial)) with { Reason = "rename", Name = "Renamed" }, userId);

        Assert.Equal("Renamed", updated.Name);
        Assert.Equal("RS-ICP-1", updated.CalibrationStandardEntryCode);
    }

    [Fact]
    public async Task SetActive_Deactivate_AuditsWithReason()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var std = await AddEntryAsync(db, section.Id, "RS-ICP-1");
        var svc = TestServiceFactory.IcpMethod(db);
        var m = await svc.CreateAsync(Req(std.Id), userId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SetActiveAsync(m.Id, false, "", userId));
        Assert.Equal("A reason is required.", ex.Message);

        var off = await svc.SetActiveAsync(m.Id, false, "No longer used", userId);

        Assert.False(off.IsActive);
        var log = Assert.Single(db.AuditLogs.Where(l => l.ActionCode == "IcpMethod.Deactivated"));
        Assert.Equal("No longer used", log.Reason);
    }

    [Fact]
    public async Task GetHistory_ReturnsBeforeAfter()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var std = await AddEntryAsync(db, section.Id, "RS-ICP-1");
        var svc = TestServiceFactory.IcpMethod(db);
        var m = await svc.CreateAsync(Req(std.Id), userId);
        await svc.UpdateAsync(m.Id, Req(std.Id, new IcpElementInput(m.Elements[0].Id, "Zn", 213.857m, AnalyteView.Axial)) with { Name = "Second", Reason = "rename" }, userId);

        var history = await svc.GetHistoryAsync(m.Id, userId);

        var updated = Assert.Single(history, h => h.Action == "IcpMethod.Updated");
        Assert.Equal("rename", updated.Reason);
        Assert.Contains("Minerals by ICP-OES", updated.BeforeJson);
        Assert.Contains("Second", updated.AfterJson);
        Assert.Null(history.Single(h => h.Action == "IcpMethod.Created").BeforeJson);
    }

    [Fact]
    public async Task GetAll_ActiveOnlyFilters()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var std = await AddEntryAsync(db, section.Id, "RS-ICP-1");
        var svc = TestServiceFactory.IcpMethod(db);
        await svc.CreateAsync(Req(std.Id), userId);
        var second = await svc.CreateAsync(Req(std.Id) with { Name = "Second", Abbreviation = "SEC-ICP" }, userId);
        await svc.SetActiveAsync(second.Id, false, "retired", userId);

        Assert.Equal(2, (await svc.GetAllAsync(userId)).Count);
        var active = await svc.GetAllAsync(userId, activeOnly: true);
        Assert.Equal("MIN-ICP", Assert.Single(active).Abbreviation);
    }
}
