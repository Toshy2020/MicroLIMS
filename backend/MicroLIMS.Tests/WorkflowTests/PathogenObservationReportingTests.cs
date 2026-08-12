using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Pdf;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// GrowthNonConforming means something grew that is NOT the organism
// under test - i.e. the target organism is ABSENT. Any reporting layer
// that collapses the three-state GrowthObservation back onto a
// growth-yes/no boolean turns that into "Detected" on a released GMP
// summary and its archived PDF. These tests pin the whole reporting
// chain - the summary projection, and the report card built from it -
// to the real three-state value.
public class PathogenObservationReportingTests
{
    private static async Task<(MicroLimsDbContext db, int sampleId)> SeedOrderWithObservationAsync(GrowthObservation observation)
    {
        var db = PathogenTestData.NewDb();
        var (order, _, _) = await PathogenTestData.SeedFiveStageOrderAsync(db);

        var role = new Role { Type = RoleType.Analyst, Name = "Analyst" };
        db.Roles.Add(role);
        var cause = new CauseOfTesting { Name = "Routine" };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();
        db.Users.Add(new User { Id = 4, FullName = "Ada Analyst", Username = "ada", RoleId = role.Id, PasswordHash = "x" });

        // Sample.CauseOfTestingId is a required relationship, and the
        // summary query Includes it - the shared pathogen fixture leaves
        // it unset, which makes the sample invisible to that query.
        var sample = await db.Samples.SingleAsync(s => s.Id == order.SampleId);
        sample.CauseOfTestingId = cause.Id;

        db.PathogenObservations.Add(new PathogenObservation
        {
            TestOrderId = order.Id,
            StepName = "Selective Plating",
            StepOrder = 3,
            Observation = observation,
            ObservedByUserId = 4,
            ObservedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        return (db, order.SampleId);
    }

    private static CardBlock TestCardOf(ReportDocument doc) =>
        doc.Blocks.OfType<CardBlock>().Single(c => c.Title.StartsWith("PATHOGEN_SALMONELLA"));

    // The projection must hand the enum's own name through untouched -
    // this is the single source of truth every downstream consumer reads.
    [Theory]
    [InlineData(GrowthObservation.NoGrowth, "NoGrowth")]
    [InlineData(GrowthObservation.GrowthNonConforming, "GrowthNonConforming")]
    [InlineData(GrowthObservation.GrowthConforming, "GrowthConforming")]
    public async Task Summary_CarriesTheObservationVerbatim(GrowthObservation observation, string expected)
    {
        var (db, sampleId) = await SeedOrderWithObservationAsync(observation);
        await using var _ = db;

        var summary = await TestServiceFactory.SampleSummary(db).GetSummaryAsync(sampleId);

        var projected = Assert.Single(summary!.TestOrders.Single().PathogenObservations);
        Assert.Equal(expected, projected.Observation);
        Assert.Equal("Ada Analyst", projected.ObservedByName);
    }

    // The defect this suite exists for: non-conforming growth rendering
    // as a detection on the archived report card.
    [Fact]
    public async Task ReportCard_NonConformingGrowthOnly_IsNotDetected()
    {
        var (db, sampleId) = await SeedOrderWithObservationAsync(GrowthObservation.GrowthNonConforming);
        await using var _ = db;

        var summary = await TestServiceFactory.SampleSummary(db).GetSummaryAsync(sampleId);
        var card = TestCardOf(ReportDocumentMapper.ForSample(summary!));

        Assert.NotEqual(ReportTone.Danger, card.Tone);
        Assert.NotEqual("Detected", card.HeadlineValue);
        Assert.Equal("Absent", card.HeadlineValue);
        Assert.Equal("Absent", card.FooterRight);
    }

    [Fact]
    public async Task ReportCard_NoGrowth_IsNotDetected()
    {
        var (db, sampleId) = await SeedOrderWithObservationAsync(GrowthObservation.NoGrowth);
        await using var _ = db;

        var summary = await TestServiceFactory.SampleSummary(db).GetSummaryAsync(sampleId);
        var card = TestCardOf(ReportDocumentMapper.ForSample(summary!));

        Assert.NotEqual(ReportTone.Danger, card.Tone);
        Assert.Equal("Absent", card.HeadlineValue);
    }

    // Positive control - a fix that excluded GrowthConforming as well
    // would make the two tests above pass while reporting every genuine
    // detection as absent, which is the far worse failure.
    [Fact]
    public async Task ReportCard_ConformingGrowth_IsDetectedWithDangerTone()
    {
        var (db, sampleId) = await SeedOrderWithObservationAsync(GrowthObservation.GrowthConforming);
        await using var _ = db;

        var summary = await TestServiceFactory.SampleSummary(db).GetSummaryAsync(sampleId);
        var card = TestCardOf(ReportDocumentMapper.ForSample(summary!));

        Assert.Equal(ReportTone.Danger, card.Tone);
        Assert.Equal("Detected", card.HeadlineValue);
        Assert.Equal("Detected", card.FooterRight);
    }

    // All three states must read distinctly on the card - merging any two
    // of them is the same defect wearing a different call site.
    [Theory]
    [InlineData(GrowthObservation.NoGrowth, "No growth")]
    [InlineData(GrowthObservation.GrowthNonConforming, "Growth observed - does not match target organism")]
    [InlineData(GrowthObservation.GrowthConforming, "Growth observed - matches target organism")]
    public async Task ReportCard_RowText_DistinguishesAllThreeStates(GrowthObservation observation, string expectedText)
    {
        var (db, sampleId) = await SeedOrderWithObservationAsync(observation);
        await using var _ = db;

        var summary = await TestServiceFactory.SampleSummary(db).GetSummaryAsync(sampleId);
        var card = TestCardOf(ReportDocumentMapper.ForSample(summary!));

        var row = Assert.Single(card.Rows);
        Assert.StartsWith(expectedText + "  |  ", row.Right);
    }
}
