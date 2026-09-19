using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Specification text shown on the sample summary / CoA for every limit type.
public class SpecificationTextFormattingTests
{
    [Fact]
    public void Formats_EachLimitType()
    {
        Assert.Equal("90.0 – 110.0 %", SampleSummaryService.FormatSpecificationText(
            new Specification { LimitType = LimitType.Range, SpecLimit = "90.0-110.0", Unit = "%" }));
        Assert.Equal("NMT 0.5 %", SampleSummaryService.FormatSpecificationText(
            new Specification { LimitType = LimitType.NotMoreThan, SpecLimit = "NMT 0.5", Unit = "%" }));
        Assert.Equal("6.0 ± 0.5 pH units", SampleSummaryService.FormatSpecificationText(
            new Specification { LimitType = LimitType.TargetWithTolerance, SpecLimit = "6.0 ± 0.5", Unit = "pH units" }));
        Assert.Equal("White round tablets", SampleSummaryService.FormatSpecificationText(
            new Specification { LimitType = LimitType.Qualitative, SpecLimit = "White round tablets", ExpectedResultText = "White round tablets" }));
        Assert.Equal("Absent in 1 g", SampleSummaryService.FormatSpecificationText(
            new Specification { LimitType = LimitType.PresenceAbsence, SpecLimit = "Absent", ExpectedState = ExpectedPresence.Absence, SampleQuantity = 1.000000m, SampleQuantityUnit = "g" }));
        Assert.Equal("Absent", SampleSummaryService.FormatSpecificationText(
            new Specification { LimitType = LimitType.PresenceAbsence, SpecLimit = "Absent", ExpectedState = ExpectedPresence.Absence }));
        Assert.Equal("Stage 1: Each unit NLT Q+5%; Stage 2: Mean NLT Q", SampleSummaryService.FormatSpecificationText(
            new Specification
            {
                LimitType = LimitType.MultiStage,
                Stages =
                {
                    new SpecificationStage { StageNumber = 2, StageLabel = "Stage 2", AcceptanceCriteriaText = "Mean NLT Q" },
                    new SpecificationStage { StageNumber = 1, StageLabel = "Stage 1", AcceptanceCriteriaText = "Each unit NLT Q+5%" }
                }
            }));
        // Count-Tiered keeps the original rule.
        Assert.Equal("NMT 100/g", SampleSummaryService.FormatSpecificationText(
            new Specification { LimitType = LimitType.CountTiered, SpecLimit = "100", Unit = "g" }));
    }

    [Fact]
    public async Task Summary_TestWithSeveralParameters_ListsThemAll_InDisplayOrder()
    {
        await using var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        db.TestDefinitions.Add(new TestDefinition { Code = "RS", DisplayName = "Related Substances", WorkflowType = WorkflowType.HplcAssay });
        var item = new Item { Name = "Vitamin C Tablets", Code = "VC-01", Category = SampleCategory.FinishedProduct };
        db.Items.Add(item);
        var cause = new CauseOfTesting { Name = "Routine" };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();

        db.Specifications.AddRange(
            new Specification { ItemId = item.Id, TestCode = "RS", ParameterName = "Total Impurities", DisplayOrder = 2, LimitType = LimitType.NotMoreThan, SpecLimit = "NMT 1.0", Unit = "%" },
            new Specification { ItemId = item.Id, TestCode = "RS", ParameterName = "Impurity A", DisplayOrder = 0, LimitType = LimitType.NotMoreThan, SpecLimit = "NMT 0.5", Unit = "%" },
            new Specification { ItemId = item.Id, TestCode = "RS", ParameterName = "Impurity B", DisplayOrder = 1, LimitType = LimitType.NotMoreThan, SpecLimit = "NMT 0.3", Unit = "%" });

        var sample = new Sample
        {
            ReferenceNumber = "FP2609001",
            Category = SampleCategory.FinishedProduct,
            ItemId = item.Id,
            CauseOfTestingId = cause.Id,
            Status = SampleStatus.Received,
            ControlNumber = "QC-1"
        };
        sample.TestOrders.Add(new TestOrder { TestCode = "RS", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var summary = await TestServiceFactory.SampleSummary(db).GetSummaryAsync(sample.Id);

        Assert.Equal("Impurity A: NMT 0.5 %; Impurity B: NMT 0.3 %; Total Impurities: NMT 1.0 %",
            Assert.Single(summary!.TestOrders).SpecificationText);
    }
}
