using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class LimitsAndUnitSpecificationTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    [Fact]
    public void SpecificationService_CompareAgainstLimits_ReturnsLimitsNotConfigured_WhenNoLimitsParse()
    {
        using var db = NewDb();
        var service = new SpecificationService(db);
        var spec = new Specification
        {
            TestCode = "TAMC",
            AlertLimit = "",
            ActionLimit = "",
            SpecLimit = ""
        };

        var result = service.CompareAgainstLimits(50, spec);
        Assert.Equal("LimitsNotConfigured", result);
    }

    [Fact]
    public void SpecificationService_CompareAgainstLimits_EvaluatesPrecedenceCorrectly()
    {
        using var db = NewDb();
        var service = new SpecificationService(db);
        var spec = new Specification
        {
            TestCode = "TAMC",
            AlertLimit = "10",
            ActionLimit = "50",
            SpecLimit = "100"
        };

        Assert.Equal("WithinLimits", service.CompareAgainstLimits(5, spec));
        Assert.Equal("AlertLimitExceeded", service.CompareAgainstLimits(15, spec));
        Assert.Equal("ActionLimitExceeded", service.CompareAgainstLimits(60, spec));
        Assert.Equal("OutOfSpecification", service.CompareAgainstLimits(150, spec));
    }

    [Fact]
    public async Task EntityPersistence_StoresAndRetrievesUnitAcrossAllFourEntities()
    {
        await using var db = NewDb();

        var item = new Item { Name = "Test Product", Code = "TP-1" };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = "TAMC",
            AlertLimit = "10",
            ActionLimit = "50",
            SpecLimit = "100",
            Unit = "g"
        };
        db.Specifications.Add(spec);

        var dept = new WaterDepartment { Name = "Utility" };
        db.WaterDepartments.Add(dept);
        await db.SaveChangesAsync();

        var point = new WaterSamplingPoint { Code = "WP-1", Location = "Room A", WaterDepartmentId = dept.Id };
        db.WaterSamplingPoints.Add(point);
        await db.SaveChangesAsync();

        var waterConfig = new SamplingConfiguration
        {
            WaterSamplingPointId = point.Id,
            TestCode = "TAMC",
            AlertLimit = "10",
            ActionLimit = "50",
            SpecLimit = "100",
            Unit = "mL"
        };
        db.SamplingConfigurations.Add(waterConfig);

        var roomDept = new Department { Name = "Cleanroom", Class = "Grade B", TestingFrequency = "Weekly" };
        db.Departments.Add(roomDept);
        await db.SaveChangesAsync();

        var room = new Room { Name = "Room 101", DepartmentId = roomDept.Id, GradeClassification = "Grade B" };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var roomConfig = new RoomTestConfiguration
        {
            RoomId = room.Id,
            TestType = "SettlePlate",
            TestCode = "TAMC",
            AlertLimit = "3",
            ActionLimit = "5",
            SpecLimit = "10",
            Unit = "plate/4h"
        };
        db.RoomTestConfigurations.Add(roomConfig);

        var machine = new Machine { Name = "Blister Machine" };
        db.Machines.Add(machine);
        await db.SaveChangesAsync();

        var part = new MachinePart { Name = "Hopper", MachineId = machine.Id };
        db.MachineParts.Add(part);
        await db.SaveChangesAsync();

        var partConfig = new MachinePartConfiguration
        {
            MachinePartId = part.Id,
            TestType = "Swab",
            TestCode = "TAMC",
            AlertLimit = "2",
            ActionLimit = "5",
            SpecLimit = "10",
            IsPathogenTest = false,
            Unit = "25cm²"
        };
        db.MachinePartConfigurations.Add(partConfig);

        await db.SaveChangesAsync();

        // Verify loaded entities retain Unit
        var loadedSpec = await db.Specifications.FirstAsync(s => s.Id == spec.Id);
        Assert.Equal("g", loadedSpec.Unit);

        var loadedWaterConfig = await db.SamplingConfigurations.FirstAsync(c => c.Id == waterConfig.Id);
        Assert.Equal("mL", loadedWaterConfig.Unit);

        var loadedRoomConfig = await db.RoomTestConfigurations.FirstAsync(c => c.Id == roomConfig.Id);
        Assert.Equal("plate/4h", loadedRoomConfig.Unit);

        var loadedPartConfig = await db.MachinePartConfigurations.FirstAsync(c => c.Id == partConfig.Id);
        Assert.Equal("25cm²", loadedPartConfig.Unit);
    }

    [Fact]
    public async Task SampleSummaryService_FormatsSpecificationText_AsNmtWhenNumericWithUnit()
    {
        await using var db = NewDb();

        db.TestDefinitions.Add(new TestDefinition { Code = "TAMC", DisplayName = "Total Aerobic Microbial Count", WorkflowType = WorkflowType.CountTest });
        db.TestDefinitions.Add(new TestDefinition { Code = "TYMC", DisplayName = "Total Yeast and Mold Count", WorkflowType = WorkflowType.CountTest });
        db.TestDefinitions.Add(new TestDefinition { Code = "ECOLI", DisplayName = "E. coli", WorkflowType = WorkflowType.Observation });

        var item = new Item { Name = "Raw Material", Code = "RM-01", Category = SampleCategory.RawMaterial };
        db.Items.Add(item);
        var cause = new CauseOfTesting { Name = "Routine" };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();

        var specWithUnit = new Specification { ItemId = item.Id, TestCode = "TAMC", SpecLimit = "100", Unit = "g" };
        var specWithoutUnit = new Specification { ItemId = item.Id, TestCode = "TYMC", SpecLimit = "50", Unit = "" };
        var specNonNumeric = new Specification { ItemId = item.Id, TestCode = "ECOLI", SpecLimit = "Absent in 10g", Unit = "" };

        db.Specifications.AddRange(specWithUnit, specWithoutUnit, specNonNumeric);

        var sample = new Sample
        {
            ReferenceNumber = "RM2608001",
            Category = SampleCategory.RawMaterial,
            ItemId = item.Id,
            CauseOfTestingId = cause.Id,
            Status = SampleStatus.Received,
            ControlNumber = "QC-1"
        };
        var order1 = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        var order2 = new TestOrder { TestCode = "TYMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        var order3 = new TestOrder { TestCode = "ECOLI", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };

        sample.TestOrders.Add(order1);
        sample.TestOrders.Add(order2);
        sample.TestOrders.Add(order3);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var service = TestServiceFactory.SampleSummary(db);
        var summary = await service.GetSummaryAsync(sample.Id);
        Assert.NotNull(summary);

        var tamcOrder = summary.TestOrders.First(t => t.TestCode == "TAMC");
        var tymcOrder = summary.TestOrders.First(t => t.TestCode == "TYMC");
        var ecoliOrder = summary.TestOrders.First(t => t.TestCode == "ECOLI");

        Assert.Equal("NMT 100/g", tamcOrder.SpecificationText);
        Assert.Equal("50", tymcOrder.SpecificationText);
        Assert.Equal("Absent in 10g", ecoliOrder.SpecificationText);
    }

    [Fact]
    public async Task ReportingQueryService_ComplianceRate_ExcludesLimitsNotConfiguredFromDenominator()
    {
        await using var db = NewDb();

        db.TestDefinitions.Add(new TestDefinition { Code = "TAMC", DisplayName = "TAMC", WorkflowType = WorkflowType.CountTest });
        await db.SaveChangesAsync();

        var date = DateTime.UtcNow;
        // 3 result records: 1 WithinLimit, 1 OutOfSpecification, 1 LimitsNotConfigured
        db.ResultRecords.AddRange(
            new ResultRecord
            {
                SourceTable = "CountTestReading", SourceId = 1,
                SampleId = 1, TestOrderId = 1, ReferenceNumber = "S1", Category = SampleCategory.FinishedProduct,
                SubjectName = "Product A", TestCode = "TAMC", TestDisplayName = "TAMC",
                ResultKind = ResultKind.Quantitative, NumericValue = 10, ReportedValue = "10",
                ResultLevel = ResultLevel.WithinLimit, ResultEnteredAt = date
            },
            new ResultRecord
            {
                SourceTable = "CountTestReading", SourceId = 2,
                SampleId = 2, TestOrderId = 2, ReferenceNumber = "S2", Category = SampleCategory.FinishedProduct,
                SubjectName = "Product A", TestCode = "TAMC", TestDisplayName = "TAMC",
                ResultKind = ResultKind.Quantitative, NumericValue = 200, ReportedValue = "200",
                ResultLevel = ResultLevel.OutOfSpecification, ResultEnteredAt = date
            },
            new ResultRecord
            {
                SourceTable = "CountTestReading", SourceId = 3,
                SampleId = 3, TestOrderId = 3, ReferenceNumber = "S3", Category = SampleCategory.FinishedProduct,
                SubjectName = "Product A", TestCode = "TAMC", TestDisplayName = "TAMC",
                ResultKind = ResultKind.Quantitative, NumericValue = 50, ReportedValue = "50",
                ResultLevel = ResultLevel.LimitsNotConfigured, ResultEnteredAt = date
            }
        );
        await db.SaveChangesAsync();

        var queryService = new ReportingQueryService(db);
        var compare = await queryService.GetCompareBySubjectAsync("TAMC", SampleCategory.FinishedProduct, date.AddDays(-1), date.AddDays(1));

        // testsEvaluated = 3 total, but compliance rate denominator should be 2 (excluding LimitsNotConfigured)
        // withinSpec = 1, so compliancePercent should be (1 / 2) * 100 = 50.0%
        var subjectSummary = compare.Subjects.First(s => s.SubjectName == "Product A");
        Assert.Equal(3, subjectSummary.TestsEvaluated);
        Assert.Equal(50.0, subjectSummary.CompliancePercent);
    }
}
