using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Json;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Enums;
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

    private static JsonSerializerOptions ApiOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApiJsonOptions.Configure(options);
        return options;
    }

    // The legacy PUT bound "weight" to a property named WorkloadWeight and saved 0 without a word.
    // These pin the exact wire names the frontend sends.
    [Fact]
    public void CreateRequest_BindsEveryWireField()
    {
        const string body = """
            {"workflowType":"CountTest","testCode":null,"workUnits":1.5,"targetAnalystDays":7,
             "effectiveFrom":"2026-10-11","reasonForChange":"SOP time study","password":"pw"}
            """;

        var r = JsonSerializer.Deserialize<CreateWorkflowProfileVersionRequest>(body, ApiOptions())!;

        Assert.Equal(WorkflowType.CountTest, r.WorkflowType);
        Assert.Null(r.TestCode);
        Assert.Equal(1.5m, r.WorkUnits);
        Assert.Equal(7m, r.TargetAnalystDays);
        Assert.Equal(new DateOnly(2026, 10, 11), r.EffectiveFrom);
        Assert.Equal("SOP time study", r.ReasonForChange);
        Assert.Equal("pw", r.Password);
    }

    [Fact]
    public void SignRequest_BindsEveryWireField()
    {
        const string body = """
            {"workUnits":null,"targetAnalystDays":9.5,"effectiveFrom":"2026-10-12","reasonForChange":"r","password":"pw"}
            """;

        var r = JsonSerializer.Deserialize<SignWorkflowProfileDraftRequest>(body, ApiOptions())!;

        Assert.Null(r.WorkUnits);
        Assert.Equal(9.5m, r.TargetAnalystDays);
        Assert.Equal(new DateOnly(2026, 10, 12), r.EffectiveFrom);
    }

    [Fact]
    public void VersionDto_SerializesCamelCaseWithStringEnumsAndIsoDate()
    {
        var dto = new WorkflowProfileVersionDto(1, WorkflowType.HplcMethodAssay, null, null, 2m, null,
            new DateOnly(2026, 10, 11), WorkflowProfileStatus.Draft, "r", null, 0, "System (legacy import)",
            new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc), null, 3);

        var json = JsonSerializer.Serialize(dto, ApiOptions());

        Assert.Contains("\"workflowType\":\"HplcMethodAssay\"", json);
        Assert.Contains("\"status\":\"Draft\"", json);
        Assert.Contains("\"effectiveFrom\":\"2026-10-11\"", json);
        Assert.Contains("\"workUnits\":2", json);
    }
}
