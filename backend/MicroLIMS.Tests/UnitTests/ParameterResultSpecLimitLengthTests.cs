using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// A qualitative result copies its specification's expected text into ParameterResult.SpecLimit.
// The result column must hold whatever the specification allows, or recording fails with a 500.
public class ParameterResultSpecLimitLengthTests
{
    [Fact]
    public void SpecLimit_HoldsTheLongestExpectedResultText()
    {
        using var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var expected = db.Model.FindEntityType(typeof(Specification))!.FindProperty(nameof(Specification.ExpectedResultText))!.GetMaxLength();
        var specLimit = db.Model.FindEntityType(typeof(ParameterResult))!.FindProperty(nameof(ParameterResult.SpecLimit))!.GetMaxLength();

        Assert.NotNull(expected);
        Assert.True(specLimit == null || specLimit >= expected,
            $"ParameterResult.SpecLimit ({specLimit}) is shorter than Specification.ExpectedResultText ({expected}).");
    }
}
