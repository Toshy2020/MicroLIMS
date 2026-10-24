using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Spc;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class ControlChartServiceTests
{
    private const string Password = "Correct-Password-1!";
    private static readonly DateTime Start = new(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);

    internal static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options) { CurrentUserId = 1 };
    }

    internal static ControlChartService NewService(MicroLimsDbContext db) =>
        new(db, new ElectronicSignatureService(db),
            new AuditEventService(db, new MicroLIMS.Persistence.Helpers.DatabaseSequenceHelper(db)));

    internal static async Task<User> SeedUserAsync(MicroLimsDbContext db)
    {
        var role = new Role { Type = RoleType.SectionHead, Name = "Section Head" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var user = new User { FullName = "Test SectionHead", Username = "sectionhead", RoleId = role.Id, PasswordHash = TestPasswords.Hash(Password) };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    internal static async Task<RoomTestConfiguration> SeedRoomAsync(MicroLimsDbContext db, string room = "Filling Room",
        string alert = "10", string action = "20", string spec = "50")
    {
        if (!await db.TestDefinitions.AnyAsync(t => t.Code == "EM_TAMC"))
            db.TestDefinitions.Add(new TestDefinition { Code = "EM_TAMC", DisplayName = "EM TAMC", SectionId = 1, WorkflowType = WorkflowType.CountTest });
        var department = new Department { Name = "Production" };
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        var r = new Room { Name = room, DepartmentId = department.Id, GradeClassification = "Grade C" };
        db.Rooms.Add(r);
        await db.SaveChangesAsync();
        var config = new RoomTestConfiguration
        {
            RoomId = r.Id, TestType = RoomTestTypes.All.First(), TestCode = "EM_TAMC",
            AlertLimit = alert, ActionLimit = action, SpecLimit = spec, Unit = "CFU/Plate"
        };
        db.RoomTestConfigurations.Add(config);
        await db.SaveChangesAsync();
        return config;
    }

    private static async Task<SamplingConfiguration> SeedWaterPointAsync(MicroLimsDbContext db)
    {
        db.TestDefinitions.Add(new TestDefinition { Code = "TAMC", DisplayName = "TAMC", SectionId = 1, WorkflowType = WorkflowType.CountTest });
        var point = new WaterSamplingPoint { Code = "WP-01", Location = "Utility Room" };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();
        var config = new SamplingConfiguration { WaterSamplingPointId = point.Id, TestCode = "TAMC", AlertLimit = "50", ActionLimit = "80", SpecLimit = "100" };
        db.SamplingConfigurations.Add(config);
        await db.SaveChangesAsync();
        return config;
    }

    internal static async Task SeedResultsAsync(MicroLimsDbContext db, string subject, string testCode, SampleCategory category,
        params double[] values)
    {
        var offset = await db.ResultRecords.CountAsync();
        for (var i = 0; i < values.Length; i++)
        {
            var sample = new Sample { Category = category, ControlNumber = $"C{offset + i}", Status = SampleStatus.Approved, SampledAt = Start.AddDays(7 * i) };
            db.Samples.Add(sample);
            await db.SaveChangesAsync();
            db.ResultRecords.Add(new ResultRecord
            {
                SampleId = sample.Id, TestOrderId = 1, SourceTable = "SampleLocation", SourceId = offset + i + 1,
                ReferenceNumber = $"REF-{offset + i}", Category = category, SubjectName = subject, TestCode = testCode,
                TestDisplayName = testCode, ResultKind = ResultKind.Quantitative, NumericValue = (decimal)values[i],
                ReportedValue = values[i].ToString(), Unit = "CFU/Plate", AlertLimit = "10", ActionLimit = "20", SpecLimit = "50",
                ResultEnteredAt = Start.AddDays(7 * i + 3), SampleStatus = SampleStatus.Approved, Round = 1
            });
        }
        await db.SaveChangesAsync();
    }

    internal static readonly double[] Counts = { 1, 3, 2, 4, 3, 2, 1, 2, 3, 2 };

    private static ControlChartQuery Query(string subject = "Filling Room", ControlChartSettings? settings = null,
        CapabilityBasis basis = CapabilityBasis.SpecLimit) =>
        new("EM_TAMC", subject, null, null, null, null, null, false, settings ?? new ControlChartSettings(), basis);

    private static ApplyTrendLimitsRequest Apply(int configId, uint version, decimal alert, decimal action,
        string kind = ControlChartService.RoomTestConfigurationKind, string subject = "Filling Room",
        SampleCategory category = SampleCategory.EnvironmentalMonitoring, string testCode = "EM_TAMC",
        string password = Password, ControlChartSettings? settings = null) =>
        new(testCode, category, null, null, false, settings ?? new ControlChartSettings(Rounding: LimitRounding.UpToWholeNumber),
            new List<ApplyLimitItem> { new(kind, configId, version, subject, alert, action) }, "Annual limit review 2026", password);

    // ---- Chart ----------------------------------------------------------

    [Fact]
    public async Task GetChart_OrdersBySamplingDate_AndReturnsTheConfigurationAsTarget()
    {
        var db = NewDb();
        var config = await SeedRoomAsync(db);
        await SeedResultsAsync(db, "Filling Room", "EM_TAMC", SampleCategory.EnvironmentalMonitoring, Counts);

        var result = await NewService(db).GetChartAsync(Query());

        Assert.Equal(Counts, result.Points.Select(p => p.Value));
        Assert.Equal(Counts.Length, result.Statistics.Count);
        var target = Assert.Single(result.LimitTargets);
        Assert.Equal(config.Id, target.ConfigurationId);
        Assert.Equal("Production · Grade C", target.Detail);
        Assert.Equal(10, result.CurrentLimits.Alert);
        Assert.Equal(50, result.CurrentLimits.SpecUpper);
        Assert.Equal(50, result.Statistics.Capability.Usl);
        Assert.NotNull(result.Statistics.Capability.Cpk);
        Assert.NotEmpty(result.Monthly);
    }

    [Fact]
    public async Task GetChart_CapabilityAgainstActionLimit_UsesTheConfiguredActionLimit()
    {
        var db = NewDb();
        await SeedRoomAsync(db);
        await SeedResultsAsync(db, "Filling Room", "EM_TAMC", SampleCategory.EnvironmentalMonitoring, Counts);

        var result = await NewService(db).GetChartAsync(Query(basis: CapabilityBasis.ActionLimit));

        Assert.Equal(20, result.Statistics.Capability.Usl);
    }

    [Fact]
    public async Task OutOfSpecResults_AreChartedButLeftOutOfTheLimits_InChartAndReview()
    {
        var db = NewDb();
        await SeedRoomAsync(db);
        await SeedResultsAsync(db, "Filling Room", "EM_TAMC", SampleCategory.EnvironmentalMonitoring, Counts.Append(1250).ToArray());
        var oos = await db.ResultRecords.SingleAsync(r => r.NumericValue == 1250);
        oos.ResultLevel = ResultLevel.OutOfSpecification;
        await db.SaveChangesAsync();
        var service = NewService(db);

        var chart = await service.GetChartAsync(Query());
        var review = await service.GetLimitReviewAsync(
            new LimitReviewQuery("EM_TAMC", SampleCategory.EnvironmentalMonitoring, null, null, false, new ControlChartSettings()));
        var included = await service.GetChartAsync(Query(settings: new ControlChartSettings(ExcludeOutOfSpec: false)));

        Assert.Equal(Counts.Length + 1, chart.Points.Count);
        Assert.False(chart.Points.Last().InCalculation);
        Assert.Equal(Counts.Length, chart.Statistics.Count);
        Assert.Equal(Counts.Average(), chart.Statistics.Mean!.Value, 6);
        Assert.Equal(chart.Statistics.Limits.ActionUpper, review.Rows.Single(r => r.SubjectName == "Filling Room").ProposedAction);
        Assert.Equal(Counts.Length + 1, included.Statistics.Count);
    }

    [Fact]
    public async Task GetChart_BelowDetectionLimit_IsPlottedAtHalfTheLimit()
    {
        var db = NewDb();
        await SeedRoomAsync(db);
        await SeedResultsAsync(db, "Filling Room", "EM_TAMC", SampleCategory.EnvironmentalMonitoring, 4);
        var record = await db.ResultRecords.SingleAsync();
        record.IsBelowDetectionLimit = true;
        record.DetectionLimit = 1;
        record.NumericValue = null;
        await db.SaveChangesAsync();

        var result = await NewService(db).GetChartAsync(Query());

        var point = Assert.Single(result.Points);
        Assert.Equal(0.5, point.Value);
        Assert.True(point.IsImputed);
    }

    [Fact]
    public async Task GetChart_RetestRound_ReplacesTheEarlierRound()
    {
        var db = NewDb();
        await SeedRoomAsync(db);
        await SeedResultsAsync(db, "Filling Room", "EM_TAMC", SampleCategory.EnvironmentalMonitoring, 40);
        var first = await db.ResultRecords.SingleAsync();
        db.ResultRecords.Add(new ResultRecord
        {
            SampleId = first.SampleId, TestOrderId = 1, SourceTable = first.SourceTable, SourceId = first.SourceId, Round = 2,
            ReferenceNumber = first.ReferenceNumber, Category = first.Category, SubjectName = first.SubjectName, TestCode = first.TestCode,
            TestDisplayName = first.TestDisplayName, ResultKind = ResultKind.Quantitative, NumericValue = 3, ReportedValue = "3",
            ResultEnteredAt = first.ResultEnteredAt.AddDays(5), SampleStatus = SampleStatus.Approved
        });
        await db.SaveChangesAsync();

        var result = await NewService(db).GetChartAsync(Query());

        Assert.Equal(3, Assert.Single(result.Points).Value);
    }

    [Fact]
    public async Task GetChart_QualitativeTest_IsRefused()
    {
        var db = NewDb();
        db.TestDefinitions.Add(new TestDefinition { Code = "ECOLI", DisplayName = "E. coli", SectionId = 1, WorkflowType = WorkflowType.Observation });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService(db).GetChartAsync(new ControlChartQuery("ECOLI", "Room", null, null, null, null, null, false, new ControlChartSettings())));
    }

    // ---- Limit review -------------------------------------------------

    [Fact]
    public async Task LimitReview_ListsEveryConfiguredLocation_WithProposedAndCurrentLimits()
    {
        var db = NewDb();
        await SeedRoomAsync(db, "Filling Room");
        await SeedRoomAsync(db, "Never Sampled Room");
        await SeedResultsAsync(db, "Filling Room", "EM_TAMC", SampleCategory.EnvironmentalMonitoring, Counts);

        var review = await NewService(db).GetLimitReviewAsync(new LimitReviewQuery(
            "EM_TAMC", SampleCategory.EnvironmentalMonitoring, null, null, false, new ControlChartSettings(Rounding: LimitRounding.UpToWholeNumber)));

        Assert.Equal(new[] { "Filling Room", "Never Sampled Room" }, review.Rows.Select(r => r.SubjectName));
        var filling = review.Rows[0];
        Assert.Equal(Counts.Length, filling.Count);
        Assert.Equal(10, filling.CurrentAlert);
        Assert.NotNull(filling.ProposedAction);
        Assert.Equal(0, review.Rows[1].Count);
        Assert.Single(review.Rows[1].LimitTargets);
    }

    // ---- Apply --------------------------------------------------------

    [Fact]
    public async Task ApplyLimits_UpdatesTheRoomConfiguration_SignsAndAudits()
    {
        var db = NewDb();
        var user = await SeedUserAsync(db);
        var config = await SeedRoomAsync(db);
        await SeedResultsAsync(db, "Filling Room", "EM_TAMC", SampleCategory.EnvironmentalMonitoring, Counts);
        var service = NewService(db);
        var review = await service.GetLimitReviewAsync(new LimitReviewQuery(
            "EM_TAMC", SampleCategory.EnvironmentalMonitoring, null, null, false, new ControlChartSettings(Rounding: LimitRounding.UpToWholeNumber)));
        var row = review.Rows.Single();

        var applied = await service.ApplyLimitsAsync(
            Apply(config.Id, config.Version, (decimal)row.ProposedAlert!.Value, (decimal)row.ProposedAction!.Value), user.Id, "127.0.0.1");

        var result = Assert.Single(applied);
        Assert.False(result.Overridden);
        Assert.Equal("10", result.PreviousAlert);
        var saved = await db.RoomTestConfigurations.SingleAsync(c => c.Id == config.Id);
        Assert.Equal(row.ProposedAlert.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), saved.AlertLimit);
        Assert.Equal(row.ProposedAction.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), saved.ActionLimit);

        var signature = await db.ElectronicSignatures.SingleAsync();
        Assert.Equal(SignatureMeaning.MasterDataChanged, signature.MeaningOfSignature);
        Assert.Equal(ControlChartService.RoomTestConfigurationKind, signature.EntityType);
        Assert.Equal(config.Id, signature.EntityId);
        Assert.Contains("Annual limit review 2026", signature.Comment);

        var audit = await db.AuditLogs.SingleAsync(a => a.ActionCode == ControlChartService.LimitsAppliedActionCode);
        Assert.Equal(config.Id.ToString(), audit.EntityId);
    }

    [Fact]
    public async Task ApplyLimits_ValuesDifferentFromTheCalculation_AreRecordedAsAnOverride()
    {
        var db = NewDb();
        var user = await SeedUserAsync(db);
        var config = await SeedRoomAsync(db);
        await SeedResultsAsync(db, "Filling Room", "EM_TAMC", SampleCategory.EnvironmentalMonitoring, Counts);

        var applied = await NewService(db).ApplyLimitsAsync(Apply(config.Id, config.Version, 8, 15), user.Id, null);

        Assert.True(Assert.Single(applied).Overridden);
        Assert.Contains("OVERRIDDEN", (await db.ElectronicSignatures.SingleAsync()).Comment);
        Assert.Equal("15", (await db.RoomTestConfigurations.SingleAsync()).ActionLimit);
    }

    [Fact]
    public async Task ApplyLimits_UpdatesAWaterSamplingConfiguration()
    {
        var db = NewDb();
        var user = await SeedUserAsync(db);
        var config = await SeedWaterPointAsync(db);
        await SeedResultsAsync(db, "WP-01", "TAMC", SampleCategory.Water, Counts);

        await NewService(db).ApplyLimitsAsync(Apply(config.Id, config.Version, 5, 7,
            kind: ControlChartService.SamplingConfigurationKind, subject: "WP-01", category: SampleCategory.Water, testCode: "TAMC"), user.Id, null);

        var saved = await db.SamplingConfigurations.SingleAsync();
        Assert.Equal("5", saved.AlertLimit);
        Assert.Equal("7", saved.ActionLimit);
        Assert.Equal("100", saved.SpecLimit);
    }

    [Fact]
    public async Task ApplyLimits_WrongPassword_ChangesNothing()
    {
        var db = NewDb();
        var user = await SeedUserAsync(db);
        var config = await SeedRoomAsync(db);

        await Assert.ThrowsAsync<SignatureVerificationException>(() =>
            NewService(db).ApplyLimitsAsync(Apply(config.Id, config.Version, 5, 7, password: "wrong"), user.Id, null));

        Assert.Equal("10", (await db.RoomTestConfigurations.AsNoTracking().SingleAsync()).AlertLimit);
    }

    [Theory]
    [InlineData(8, 5)]   // alert above action
    [InlineData(5, 60)]  // action above the 50 spec limit
    [InlineData(-1, 5)]  // negative
    public async Task ApplyLimits_InvalidLimits_AreRefused(double alert, double action)
    {
        var db = NewDb();
        var user = await SeedUserAsync(db);
        var config = await SeedRoomAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService(db).ApplyLimitsAsync(Apply(config.Id, config.Version, (decimal)alert, (decimal)action), user.Id, null));
        Assert.Empty(db.ElectronicSignatures);
    }

    [Fact]
    public async Task ApplyLimits_StaleVersion_IsRefused()
    {
        var db = NewDb();
        var user = await SeedUserAsync(db);
        var config = await SeedRoomAsync(db);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            NewService(db).ApplyLimitsAsync(Apply(config.Id, config.Version + 1, 5, 7), user.Id, null));
    }

    [Fact]
    public async Task ApplyLimits_ConfigurationOfAnotherLocation_IsRefused()
    {
        var db = NewDb();
        var user = await SeedUserAsync(db);
        var config = await SeedRoomAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService(db).ApplyLimitsAsync(Apply(config.Id, config.Version, 5, 7, subject: "Other Room"), user.Id, null));
    }

    [Fact]
    public async Task ApplyLimits_ProductCategory_IsRefused()
    {
        var db = NewDb();
        var user = await SeedUserAsync(db);
        var config = await SeedRoomAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService(db).ApplyLimitsAsync(Apply(config.Id, config.Version, 5, 7, category: SampleCategory.FinishedProduct), user.Id, null));
    }

    [Fact]
    public async Task ApplyLimits_WithoutReason_IsRefused()
    {
        var db = NewDb();
        var user = await SeedUserAsync(db);
        var config = await SeedRoomAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService(db).ApplyLimitsAsync(Apply(config.Id, config.Version, 5, 7) with { Reason = "  " }, user.Id, null));
    }
}
