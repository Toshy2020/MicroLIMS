using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class ResultMatrixBuilderTests
{
    private static SessionAssignedTestDto Test(int orderId, string code, string workflowType, bool entryAllowed, string? lockReason = null, int mediaCount = 1) =>
        new(orderId, code, code + " name", workflowType, "InProgress", "Start", null, false,
            "STATE", "State", entryAllowed, !entryAllowed, lockReason, new List<SessionWorkflowStepDto>(), mediaCount);

    private static SessionLocationDto Location(int id, int primaryLocId, string name, params (string Code, int LocId)[] map) =>
        new(id, primaryLocId, name, "Room", null, map.ToDictionary(m => m.Code, m => m.LocId));

    private static ResultMatrix Build(
        SessionLocationDto[] locations, SessionAssignedTestDto[] tests, Sample sample,
        Dictionary<(int, int), LocationPathogenObservation>? obs = null) =>
        ResultMatrixBuilder.Build(locations, tests, sample, obs ?? new(), id => id == 5 ? "Mazen" : "Unknown");

    [Fact]
    public void EnteredQuantitativeResult_IsCompleted_WithValueAndEnteredBy()
    {
        var sample = new Sample { Locations = { new SampleLocation { Id = 10, ReportedResult = "12", CFUResult = 12m, Status = ResultStatus.WithinLimits, EnteredByUserId = 5, EnteredAt = new DateTime(2026, 10, 9) } } };

        var r = Build(new[] { Location(1, 10, "Room A", ("TAMC", 10)) }, new[] { Test(1, "TAMC", "CountTest", entryAllowed: true) }, sample);

        var cell = Assert.Single(r.Matrix);
        Assert.Equal("COMPLETED", cell.CellState);
        Assert.False(cell.IsEditable);
        Assert.Equal("12", cell.ResultCode);
        Assert.Equal(12m, cell.NumericValue);
        Assert.Equal("Quantitative", cell.ResultType);
        Assert.Equal("Mazen", cell.EnteredByUserName);
        Assert.Equal((1, 0, 0), (r.Completed, r.Available, r.Locked));
        Assert.Empty(r.Missing);
    }

    [Fact]
    public void EmptyCell_IsAvailableWhenEntryAllowed_AndLockedWithAReasonOtherwise()
    {
        var sample = new Sample { Locations = { new SampleLocation { Id = 10 }, new SampleLocation { Id = 11 } } };
        var locations = new[] { Location(1, 10, "Room A", ("TAMC", 10), ("SALMONELLA", 11)) };
        var tests = new[] { Test(1, "TAMC", "CountTest", true), Test(2, "SALMONELLA", "Pathogen", false, "Waiting for TSB") };

        var r = Build(locations, tests, sample);

        var open = r.Matrix.Single(c => c.TestCode == "TAMC");
        Assert.Equal("AVAILABLE", open.CellState);
        Assert.True(open.IsEditable);
        var locked = r.Matrix.Single(c => c.TestCode == "SALMONELLA");
        Assert.Equal("LOCKED_PREREQUISITE", locked.CellState);
        Assert.Equal("Waiting for TSB", locked.LockReason);
        Assert.Equal((0, 1, 1), (r.Completed, r.Available, r.Locked));
        Assert.Equal(2, r.Missing.Count);
    }

    [Fact]
    public void LockedCellWithoutAReason_GetsTheDefaultText()
    {
        var sample = new Sample { Locations = { new SampleLocation { Id = 10 } } };

        var r = Build(new[] { Location(1, 10, "Room A", ("SALMONELLA", 10)) }, new[] { Test(1, "SALMONELLA", "Pathogen", false) }, sample);

        Assert.Equal("Locked until prerequisite is complete", Assert.Single(r.Matrix).LockReason);
    }

    [Fact]
    public void QualitativeResult_IsDetectedOnlyForADetectedText()
    {
        var sample = new Sample { Locations = { new SampleLocation { Id = 10, ReportedResult = "Detected (+)", Status = ResultStatus.Detected }, new SampleLocation { Id = 11, ReportedResult = "Not detected", Status = ResultStatus.WithinLimits } } };
        var locations = new[] { Location(1, 10, "A", ("SALMONELLA", 10)), Location(2, 11, "B", ("SALMONELLA", 11)) };

        var r = Build(locations, new[] { Test(1, "SALMONELLA", "Pathogen", true) }, sample);

        Assert.Equal("DETECTED", r.Matrix.Single(c => c.LocationName == "A").ResultCode);
        Assert.Equal("NOT_DETECTED", r.Matrix.Single(c => c.LocationName == "B").ResultCode);
        Assert.All(r.Matrix, c => Assert.Equal("Qualitative", c.ResultType));
    }

    [Fact]
    public void PrimaryNoGrowth_IsNotApplicableForConfirmation_AndGrowthIsEligible()
    {
        var sample = new Sample { Locations = { new SampleLocation { Id = 10 }, new SampleLocation { Id = 11 } } };
        var locations = new[] { Location(1, 10, "A", ("SALMONELLA", 10)), Location(2, 11, "B", ("SALMONELLA", 11)) };
        var obs = new Dictionary<(int, int), LocationPathogenObservation>
        {
            [(10, 1)] = new() { Id = 100, SampleLocationId = 10, TestOrderId = 1, GrowthObservation = GrowthObservation.NoGrowth },
            [(11, 1)] = new() { Id = 101, SampleLocationId = 11, TestOrderId = 1, GrowthObservation = GrowthObservation.GrowthConforming }
        };

        var r = Build(locations, new[] { Test(1, "SALMONELLA", "Pathogen", true, mediaCount: 2) }, sample, obs);

        var none = r.Matrix.Single(c => c.LocationName == "A");
        Assert.Equal("NotApplicable", none.ConfirmationStatus);
        Assert.False(none.IsEligibleForConfirmation);
        var grew = r.Matrix.Single(c => c.LocationName == "B");
        Assert.Equal("Eligible", grew.ConfirmationStatus);
        Assert.True(grew.IsEligibleForConfirmation);
        Assert.Equal(101, grew.PrimaryObservationId);
    }
}
