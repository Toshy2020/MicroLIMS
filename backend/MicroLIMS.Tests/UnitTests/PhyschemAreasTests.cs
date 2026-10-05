using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class PhyschemAreasTests
{
    [Theory]
    [InlineData(SampleCategory.RawMaterial, WorkspaceArea.RmPm)]
    [InlineData(SampleCategory.PackagingMaterial, WorkspaceArea.RmPm)]
    [InlineData(SampleCategory.FinishedProduct, WorkspaceArea.Fp)]
    [InlineData(SampleCategory.Water, WorkspaceArea.Fp)]
    public void OfCategory_MapsRmPmAndEverythingElseToFp(SampleCategory c, WorkspaceArea expected) =>
        Assert.Equal(expected, PhyschemAreas.OfCategory(c));

    [Theory]
    [InlineData(null, WorkspaceArea.Fp, true)]
    [InlineData(null, WorkspaceArea.RmPm, true)]
    [InlineData(PhyschemArea.Both, WorkspaceArea.Fp, true)]
    [InlineData(PhyschemArea.Both, WorkspaceArea.RmPm, true)]
    [InlineData(PhyschemArea.FinishedProduct, WorkspaceArea.Fp, true)]
    [InlineData(PhyschemArea.FinishedProduct, WorkspaceArea.RmPm, false)]
    [InlineData(PhyschemArea.RawPackaging, WorkspaceArea.RmPm, true)]
    [InlineData(PhyschemArea.RawPackaging, WorkspaceArea.Fp, false)]
    public void Includes_FollowsTestArea(PhyschemArea? test, WorkspaceArea area, bool expected) =>
        Assert.Equal(expected, PhyschemAreas.Includes(test, area));

    [Fact]
    public void CategoriesOf_SplitsAllCategoriesWithoutOverlap()
    {
        var rm = PhyschemAreas.CategoriesOf(WorkspaceArea.RmPm);
        var fp = PhyschemAreas.CategoriesOf(WorkspaceArea.Fp);
        Assert.Equal(new[] { SampleCategory.RawMaterial, SampleCategory.PackagingMaterial }, rm);
        Assert.Empty(rm.Intersect(fp));
        Assert.Equal(Enum.GetValues<SampleCategory>().Length, rm.Count + fp.Count);
    }

    [Theory]
    [InlineData("fp", WorkspaceArea.Fp)]
    [InlineData("RMPM", WorkspaceArea.RmPm)]
    public void Parse_AcceptsKnownValues(string s, WorkspaceArea expected) => Assert.Equal(expected, PhyschemAreas.Parse(s));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Parse_EmptyIsNull(string? s) => Assert.Null(PhyschemAreas.Parse(s));

    [Fact]
    public void Parse_UnknownThrows() => Assert.Throws<InvalidOperationException>(() => PhyschemAreas.Parse("x"));

    [Fact]
    public void Grants_NullMeansBoth()
    {
        Assert.Equal(new[] { WorkspaceArea.Fp, WorkspaceArea.RmPm }, PhyschemAreas.Grants(null));
        Assert.Equal(new[] { WorkspaceArea.Fp, WorkspaceArea.RmPm }, PhyschemAreas.Grants(PhyschemArea.Both));
        Assert.Equal(new[] { WorkspaceArea.Fp }, PhyschemAreas.Grants(PhyschemArea.FinishedProduct));
        Assert.Equal(new[] { WorkspaceArea.RmPm }, PhyschemAreas.Grants(PhyschemArea.RawPackaging));
    }
}
