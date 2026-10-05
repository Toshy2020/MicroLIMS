using MicroLIMS.Application.Abstractions.Pdf;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// The report archived at approval judges each test with the same rule as
// the Certificate of Analysis (TestOrderConformance). Its own copy of the
// rule passed a location with no result or awaiting confirmation, showed a
// count reading over the alert limit as a pass, and never looked at
// elemental-assay or measured-parameter results at all.
public class ReportConformanceTests
{
    private static CardBlock CardFor(TestOrderSummaryDetailDto order)
    {
        order.TestCode = "T1";
        order.TestDisplayName = "Test";
        var summary = new SampleSummaryDto { Status = "Approved", OverallStatus = "Approved", TestOrders = { order } };
        return ReportDocumentMapper.ForSample(summary).Blocks.OfType<CardBlock>().Single(c => c.Title.StartsWith("T1"));
    }

    private static TestOrderSummaryDetailDto WithLocations(params ResultStatus?[] statuses) => new()
    {
        Locations = statuses.Select((s, i) => new SampleLocationDetailDto { LocationKey = $"{i}", LocationName = $"Room {i}", Status = s }).ToList()
    };

    [Fact]
    public void AllLocationsWithinLimits_IsAPass()
    {
        var card = CardFor(WithLocations(ResultStatus.WithinLimits, ResultStatus.Absent));

        Assert.Equal(ReportTone.Positive, card.Tone);
        Assert.Equal("All locations within spec", card.FooterRight);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(ResultStatus.LimitsNotConfigured)]
    public void LocationThatCannotBeCertified_IsNotShownAsAPass(ResultStatus? status)
    {
        var card = CardFor(WithLocations(ResultStatus.WithinLimits, status));

        Assert.Equal(ReportTone.Warning, card.Tone);
        Assert.Equal("Not all locations can be certified", card.FooterRight);
    }

    [Fact]
    public void LocationAwaitingConfirmation_IsNotAPass()
    {
        var card = CardFor(WithLocations(ResultStatus.WithinLimits, ResultStatus.PendingConfirmation));

        Assert.Equal(ReportTone.Danger, card.Tone);
        Assert.Equal("Non-conforming location(s)", card.FooterRight);
    }

    [Theory]
    [InlineData(ResultStatus.AlertLimitExceeded)]
    [InlineData(ResultStatus.RequiresReview)]
    [InlineData(ResultStatus.OutOfSpecification)]
    public void CountReadingOutsideItsLimits_IsAFailure(ResultStatus status)
    {
        var card = CardFor(new TestOrderSummaryDetailDto
        {
            CountTestReadings = { new CountTestReadingDetailDto { ReportedResult = "50", Status = status } }
        });

        Assert.Equal(ReportTone.Danger, card.Tone);
    }

    [Fact]
    public void BiochemicalAbsence_OverridesConformingGrowthAtPlating()
    {
        var card = CardFor(new TestOrderSummaryDetailDto
        {
            PathogenObservations = { new PathogenObservationDetailDto { Observation = "GrowthConforming" } },
            BiochemicalResults = { new BiochemicalResultDetailDto { OrganismDetected = false } }
        });

        Assert.Equal(ReportTone.Positive, card.Tone);
        Assert.Equal(nameof(ResultStatus.Absent), card.HeadlineValue);
    }
}
