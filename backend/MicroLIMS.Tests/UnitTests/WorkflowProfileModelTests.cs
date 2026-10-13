using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Json;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// DateOnly has no precedent in the Domain layer. These tests prove the two
// places it could go wrong before anything else is built on it.
public class WorkflowProfileModelTests
{
    [Fact]
    public void WorkflowProfileVersions_MapsToTheExpectedColumns()
    {
        // GenerateCreateScript builds the Npgsql model without opening a connection.
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=x;Password=x")
            .Options;
        using var db = new MicroLimsDbContext(options);

        var script = db.Database.GenerateCreateScript();
        var start = script.IndexOf("CREATE TABLE \"WorkflowProfileVersions\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "WorkflowProfileVersions is not part of the model.");
        var block = script.Substring(start, script.IndexOf(");", start, StringComparison.Ordinal) - start);

        Assert.Contains("\"EffectiveFrom\" date NOT NULL", block);
        Assert.Contains("\"WorkUnits\" numeric(8,2)", block);
        Assert.Contains("\"TargetAnalystDays\" numeric(6,2)", block);
        Assert.Contains("\"TestCode\" character varying(50)", block);
        Assert.Contains("\"ReasonForChange\" character varying(500) NOT NULL", block);
        Assert.Contains("CK_WorkflowProfileVersions_UnitsPositive", block);
        Assert.Contains("CK_WorkflowProfileVersions_TargetPositive", block);
        Assert.Contains("CK_WorkflowProfileVersions_AnyValue", block);
        Assert.Contains("IX_WorkflowProfileVersions_WorkflowType_TestCode_EffectiveFrom", script);
    }

    private sealed record DateHolder(DateOnly EffectiveFrom);

    [Fact]
    public void DateOnly_RoundTripsAsIsoDate_ThroughTheApiJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApiJsonOptions.Configure(options);

        var json = JsonSerializer.Serialize(new DateHolder(new DateOnly(2026, 10, 11)), options);
        Assert.Equal("{\"effectiveFrom\":\"2026-10-11\"}", json);

        var back = JsonSerializer.Deserialize<DateHolder>("{\"effectiveFrom\":\"2026-10-11\"}", options);
        Assert.Equal(new DateOnly(2026, 10, 11), back!.EffectiveFrom);
    }
}
