using MicroLIMS.Application.Abstractions.Pdf;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Pdf;
using UglyToad.PdfPig;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// The exported/archived sample PDF is the single controlled report: it has
// to carry everything the on-screen printable report showed (results
// matrix, storage, After Cleaning traceability, the full lifecycle,
// incubation detail, analysis results and readings) and print every value
// in full rather than cutting it off.
public class SampleReportDocumentTests
{
    private static readonly DateTime T0 = new(2026, 9, 15, 18, 35, 0);

    private static SampleSummaryDto FinishedProduct() => new()
    {
        ReferenceNumber = "FP0926120",
        Category = "FinishedProduct",
        DisplayName = "Wellman Original",
        BatchNumber = "26021072",
        ControlNumber = "S26090103",
        CauseOfTesting = "Routine",
        Status = "Approved",
        ReceivedByName = "Ahmed shawky",
        ReceivedAt = T0,
        SampledBy = "IPQA",
        StorageCondition = "Refrigerator",
        StorageTimeHours = 24,
        TestOrders =
        {
            new TestOrderSummaryDetailDto
            {
                TestCode = "Salmonella", TestDisplayName = "Salmonella typhimurium", Status = "Approved", CurrentStep = "XLD",
                Incubations =
                {
                    new IncubationDetailDto { StepName = "TSB", MediaLotNumber = "TSB/03/26", MediaMaterialName = "Tryptic Soy Broth", IncubatorName = "Labtop", Temperature = "30.00-35.00 °C", StartedAt = T0.AddMinutes(39), StartedByName = "Ahmed shawky", CompletedAt = T0.AddDays(1), CompletedByName = "Nadeen Mohamed" },
                    new IncubationDetailDto { StepName = "RVS", MediaLotNumber = "RVS/01/26", IncubatorName = "Labtop", Temperature = "30.00-35.00 °C", StartedAt = T0.AddDays(4), StartedByName = "Ahmed shawky" },
                    new IncubationDetailDto { StepName = "XLD", MediaLotNumber = "XLD/09/26", IncubatorName = "Labtop", Temperature = "30.00-35.00 °C", StartedAt = T0.AddDays(6), StartedByName = "Ahmed shawky" }
                },
                PathogenObservations = { new PathogenObservationDetailDto { StepName = "XLD", StepOrder = 3, Observation = "NoGrowth", ObservedByName = "Ahmed shawky", ObservedAt = T0.AddDays(10) } }
            }
        },
        Timeline =
        {
            new SampleWorkflowEventDto { EventType = "ApprovalDecisionMade", Decision = "Approve", PerformedByName = "Mohamed Mahmoud", Timestamp = T0.AddDays(12) },
            new SampleWorkflowEventDto { EventType = "SubmittedForReview", PerformedByName = "Ahmed shawky", Timestamp = T0.AddDays(10), Comment = "All tests completed" }
        },
        Signatures = { new SignatureDto("Mohamed Mahmoud", "MMASH", "SectionHead", "Approved", T0.AddDays(12), null) }
    };

    private static string PdfText(ReportDocument doc)
    {
        using var pdf = PdfDocument.Open(new ReportPdfRenderer().Render(doc));
        return string.Join("\n", pdf.GetPages().Select(p => string.Join(" ", p.GetWords().Select(w => w.Text))));
    }

    [Fact]
    public void Identity_ShowsStorage_AndAfterCleaningTraceabilityInPlaceOfBatch()
    {
        var fp = ReportDocumentMapper.ForSample(FinishedProduct()).Blocks.OfType<TwoColumnBlock>().Single();
        Assert.Contains(("Storage", "Refrigerator (24h)"), fp.Left);
        Assert.Contains(("Batch", "26021072"), fp.Left);

        var ac = FinishedProduct();
        ac.Category = "AfterCleaning";
        ac.PreviousProductName = "Wellman Plus";
        ac.PreviousProductBatchNumber = "B-77";
        var identity = ReportDocumentMapper.ForSample(ac).Blocks.OfType<TwoColumnBlock>().Single().Left;
        Assert.Contains(("Prev. product", "Wellman Plus"), identity);
        Assert.Contains(("Prev. batch", "B-77"), identity);
        Assert.DoesNotContain(identity, p => p.Item1 == "Batch");
    }

    [Fact]
    public void Timeline_RunsFromReceiptThroughEveryEvent_InTimeOrder()
    {
        var timeline = ReportDocumentMapper.ForSample(FinishedProduct()).Blocks.OfType<ListBlock>().Single(b => b.Label == "lifecycle timeline");

        Assert.Equal(new[] { "Received", "Testing started", "Submitted for review", "Approval decision made - Approve" },
            timeline.Rows.Select(r => r.Left));
        Assert.Contains("\"All tests completed\"", timeline.Rows[2].Right);
    }

    [Fact]
    public void IncubationTable_NamesEachStep_AndKeepsOperatorNames()
    {
        var card = ReportDocumentMapper.ForSample(FinishedProduct()).Blocks.OfType<CardBlock>().Single();
        var table = Assert.Single(card.Tables);

        Assert.Equal(new[] { "TSB", "RVS", "XLD" }, table.Rows.Select(r => r[0]));
        Assert.Equal("TSB/03/26\nTryptic Soy Broth", table.Rows[0][1]);
        Assert.Equal("15-Sep-2026 19:14\nAhmed shawky", table.Rows[0][5]);
        Assert.Equal("16-Sep-2026 18:35\nNadeen Mohamed", table.Rows[0][6]);
    }

    [Fact]
    public void IncubationTable_NamesTheStage_OnlyForATransferredStep()
    {
        var sample = FinishedProduct();
        var incubations = sample.TestOrders[0].Incubations;
        incubations.Clear();
        incubations.Add(new IncubationDetailDto { StepName = "SDA", StageNumber = 1, StartedAt = T0, TransferredAt = T0.AddDays(2), TransferredByName = "Ahmed shawky" });
        incubations.Add(new IncubationDetailDto { StepName = "SDA", StageNumber = 2, StartedAt = T0.AddDays(2) });

        var table = ReportDocumentMapper.ForSample(sample).Blocks.OfType<CardBlock>().Single().Tables.Single();

        Assert.Equal("SDA\nStage 1", table.Rows[0][0]);
        Assert.Equal("SDA\nStage 2", table.Rows[1][0]);
        Assert.EndsWith("(transferred)", table.Rows[0][6]);
    }

    [Fact]
    public void ResultsMatrix_CrossesLocatedTestsWithLocations()
    {
        var sample = FinishedProduct();
        sample.TestOrders.Add(new TestOrderSummaryDetailDto
        {
            TestCode = "TAMC",
            Locations =
            {
                new SampleLocationDetailDto { LocationKey = "1", LocationName = "Room A", CFUResult = 3, Unit = "CFU/plate", Status = ResultStatus.WithinLimits },
                new SampleLocationDetailDto { LocationKey = "2", LocationName = "Room A", CFUResult = 90, Unit = "CFU/plate", Status = ResultStatus.ActionLimitExceeded }
            }
        });

        var matrix = ReportDocumentMapper.ForSample(sample).Blocks.OfType<TableBlock>().Single();

        Assert.Equal(new[] { "Location", "TAMC (CFU/plate)" }, matrix.Table.Columns.Select(c => c.Header));
        Assert.Equal(2, matrix.Table.Rows.Count); // same name, distinct keys
        Assert.Equal("3", matrix.Table.Rows[0][1]);
        Assert.Equal("90\nAction limit exceeded", matrix.Table.Rows[1][1]);
        Assert.Equal(ReportTone.Danger, matrix.Table.CellTones![1][1]);
        Assert.Equal("1 exception", matrix.BadgeText);
    }

    [Fact]
    public void AnalysisTest_GetsParameterTable_AndReadingsTableAfterTheCard()
    {
        var sample = FinishedProduct();
        sample.TestOrders.Add(new TestOrderSummaryDetailDto
        {
            TestCode = "WV", TestDisplayName = "Weight variation", Status = "Approved",
            Analysis = new AnalysisDetailDto
            {
                AnalysisType = WorkflowType.WeightVariation, EquipmentCode = "BAL-01", EnteredByName = "Ahmed shawky",
                ParameterResults =
                {
                    new ParameterResultDetailDto
                    {
                        ParameterName = "Average weight", ReportedDisplay = "501.2 mg", Unit = "mg", SpecLimit = "475 - 525",
                        ComparisonStatus = ResultStatus.WithinLimits,
                        Readings = { new ResultReadingDetailDto { Index = 1, Value1 = 501.2m, Value3 = 0.04m } }
                    }
                }
            }
        });

        var blocks = ReportDocumentMapper.ForSample(sample).Blocks;
        var card = blocks.OfType<CardBlock>().Single(c => c.Title.StartsWith("WV"));
        var readings = blocks.OfType<TableBlock>().Single(b => b.Label.StartsWith("WV readings"));

        Assert.Equal("501.2 mg", card.HeadlineValue);
        Assert.Equal(new[] { "Average weight", "501.2", "mg", "475 - 525", "WithinLimits" }, card.TableRows.Single());
        Assert.Equal(new[] { "#", "Weight (mg)", "Deviation %" }, readings.Table.Columns.Select(c => c.Header));
        Assert.Equal(new[] { "1", "501.2", "0.04 %" }, readings.Table.Rows.Single());
        Assert.Equal(blocks.IndexOf(card) + 1, blocks.IndexOf(readings));
    }

    [Fact]
    public void SignatureMeaning_ComesFromTheSharedVocabulary()
    {
        var sig = ReportDocumentMapper.ForSample(FinishedProduct()).Blocks.OfType<SignatureBlock>().Single().Signatures.Single();

        Assert.Equal("I approve the release of this record for its intended use.", sig.Meaning);
        Assert.Equal(sig.Meaning, FinishedProduct().Signatures[0].MeaningText);
        Assert.Equal("In testing", new SampleSummaryDto { Status = "InTesting" }.StatusDisplay);
    }

    [Fact]
    public void RenderedPdf_PrintsIncubationValuesInFull()
    {
        var text = PdfText(ReportDocumentMapper.ForSample(FinishedProduct()));

        Assert.Contains("Tryptic Soy Broth", text);
        Assert.Contains("Nadeen Mohamed", text);
        Assert.DoesNotContain("...", text);
    }

    [Fact]
    public void LongReadingsTable_BreaksAcrossPages_RepeatingItsHeader()
    {
        var doc = new ReportDocument { Kind = "Test", DocumentId = "X1" };
        doc.Blocks.Add(new TableBlock
        {
            Label = "readings",
            Table = new ReportTable
            {
                Columns = { new("#", 0.2), new("Value", 0.8) },
                Rows = Enumerable.Range(1, 150).Select(i => new List<string> { $"{i}", $"V{i}" }).ToList()
            }
        });

        using var pdf = PdfDocument.Open(new ReportPdfRenderer().Render(doc));
        var pages = pdf.GetPages().Select(p => string.Join(" ", p.GetWords().Select(w => w.Text))).ToList();

        Assert.True(pages.Count >= 3);
        Assert.All(pages, p => Assert.Contains("VALUE", p));
        Assert.Contains("V150", pages[^1]);
    }
}
