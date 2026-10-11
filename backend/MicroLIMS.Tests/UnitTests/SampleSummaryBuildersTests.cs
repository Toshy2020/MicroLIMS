using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class SampleSummaryBuildersTests
{
    private static Sample SampleWithSignoffs() => new()
    {
        Id = 1,
        ReceivedByUserId = 1,
        SectionSignoffs =
        {
            new SampleSectionSignoff { SectionId = 10, Status = SectionSignoffStatus.Approved, ReviewedByUserId = 2, ApprovedByUserId = 3 },
            new SampleSectionSignoff { SectionId = 20, Status = SectionSignoffStatus.Approved, ReviewedByUserId = 4, ApprovedByUserId = 5 }
        }
    };

    private static readonly Dictionary<int, SampleSectionRow> Rows = new() { [10] = new("MB", "Micro"), [20] = new("FP", "Finished Product") };

    [Fact]
    public void Sections_HideReviewerAndApprover_OfSectionsTheViewerCannotSee()
    {
        var sections = SampleSummarySectionsBuilder.Build(SampleWithSignoffs(), new[] { 10 }, Rows, id => $"User {id}");

        var visible = sections.Single(s => s.SectionId == 10);
        var hidden = sections.Single(s => s.SectionId == 20);
        Assert.True(visible.CanView);
        Assert.Equal("User 2", visible.ReviewedByName);
        Assert.False(hidden.CanView);
        Assert.Null(hidden.ReviewedByName);
        Assert.Null(hidden.ApprovedByName);
    }

    [Fact]
    public void Sections_UnrestrictedViewer_SeesEverySection()
    {
        var sections = SampleSummarySectionsBuilder.Build(SampleWithSignoffs(), null, Rows, id => $"User {id}");

        Assert.All(sections, s => Assert.True(s.CanView));
    }

    [Fact]
    public void UserIds_IncludeSectionSignoffUsersAndPreparer()
    {
        var sample = SampleWithSignoffs();
        var data = new SampleSummaryData
        {
            Sample = sample, EffectiveTestOrders = new(), SourceRefByOrderId = new(), SectionRows = Rows, Incubations = new(), Results = new(),
            CountTestReadings = new(), ActiveAnalyses = new(), PathogenObservations = new(), BiochemicalResults = new(), WorkflowHistory = new(),
            SampleLocations = new(), LocationPathogenObservations = new(), TestDefinitions = new(), MediaLookup = new(),
            SpecificationsByTestCode = new(), Now = DateTime.UtcNow
        };

        var ids = SampleSummaryUserIds.Collect(data, new SamplePreparation { PreparedByUserId = 9 });

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 9 }, ids.OrderBy(i => i));
    }
}
