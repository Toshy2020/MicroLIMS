using System.Reflection;
using Xunit;

namespace MicroLIMS.Tests.ArchitectureTests;

// Clean Architecture: dependencies point inwards. Domain depends on no
// other MicroLIMS project; Application only on Domain and Shared, and
// reaches the database, files, e-mail and documents through its own
// Abstractions, which Persistence and Infrastructure implement.
public class LayerDependencyTests
{
    private static readonly Assembly Domain = typeof(MicroLIMS.Domain.Entities.Sample).Assembly;
    private static readonly Assembly Application = typeof(MicroLIMS.Application.Abstractions.Persistence.IMicroLimsDbContext).Assembly;
    private static readonly Assembly Persistence = typeof(MicroLIMS.Persistence.DbContext.MicroLimsDbContext).Assembly;
    private static readonly Assembly Infrastructure = typeof(MicroLIMS.Infrastructure.Pdf.PdfGenerator).Assembly;
    private static readonly Assembly Shared = typeof(MicroLIMS.Shared.Exceptions.BusinessRuleException).Assembly;

    private static string[] MicroLimsReferencesOf(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("MicroLIMS.", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

    [Fact]
    public void Domain_DependsOnNoOtherProject() =>
        Assert.Empty(MicroLimsReferencesOf(Domain));

    [Fact]
    public void Shared_DependsOnNoOtherProject() =>
        Assert.Empty(MicroLimsReferencesOf(Shared));

    [Fact]
    public void Application_DependsOnlyOnDomainAndShared() =>
        Assert.Equal(new[] { "MicroLIMS.Domain", "MicroLIMS.Shared" }, MicroLimsReferencesOf(Application));

    [Fact]
    public void Application_DoesNotReferenceTheDatabaseProvider() =>
        Assert.DoesNotContain(Application.GetReferencedAssemblies(), a => a.Name!.StartsWith("Npgsql", StringComparison.Ordinal));

    [Theory]
    [MemberData(nameof(OuterLayers))]
    public void PersistenceAndInfrastructure_DoNotDependOnTheApi(string name)
    {
        var assembly = name == "Persistence" ? Persistence : Infrastructure;
        Assert.DoesNotContain("MicroLIMS.API", MicroLimsReferencesOf(assembly));
    }

    [Fact]
    public void PersistenceAndInfrastructure_DoNotDependOnEachOther()
    {
        Assert.DoesNotContain("MicroLIMS.Infrastructure", MicroLimsReferencesOf(Persistence));
        Assert.DoesNotContain("MicroLIMS.Persistence", MicroLimsReferencesOf(Infrastructure));
    }

    public static TheoryData<string> OuterLayers => new() { "Persistence", "Infrastructure" };
}
