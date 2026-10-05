using System.Text.Json;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// The test-master request records have more than 64 constructor parameters,
// which System.Text.Json cannot bind through the constructor. Every Add/Edit
// Test then failed model binding with a 400 the UI showed only as "Could not
// add this test.". These read a body the way the API does.
public class TestDefinitionRequestBindingTests
{
    private static JsonSerializerOptions ApiOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        MicroLIMS.API.Json.ApiJsonOptions.Configure(options);
        return options;
    }

    private const string TitrationBody = """
        {
          "code": "Titration by 0.1 HCL", "displayName": "Assay by titration", "sectionId": 7,
          "workflowType": "Titration", "equationType": "Titration", "requiresSystemSuitability": false,
          "replicateCount": 3, "titrationType": "AcidBase", "titrationNonAqueous": false,
          "titrationMode": "Direct", "titrationCalculation": "UspFactor", "titrantSolutionMasterId": 12,
          "titrationEquivalencyFactor": 1.012, "titrationBlankRequired": false, "titrationMaxRsdPercent": 2,
          "titrationEndpoint": "Visual", "titrationIndicatorEntryId": 5, "titrationStandardEntryId": null
        }
        """;

    [Fact]
    public void CreateRequest_BindsFromJson()
    {
        var r = JsonSerializer.Deserialize<CreateTestDefinitionRequest>(TitrationBody, ApiOptions())!;
        Assert.Equal("Titration by 0.1 HCL", r.Code);
        Assert.Equal(WorkflowType.Titration, r.WorkflowType);
        Assert.Equal(EquationType.Titration, r.EquationType);
        Assert.Equal(TitrationType.AcidBase, r.TitrationType);
        Assert.Equal(1.012m, r.TitrationEquivalencyFactor);
        Assert.Equal(5, r.TitrationIndicatorEntryId);
        Assert.Equal(3, r.ReplicateCount);
    }

    [Fact]
    public void UpdateRequest_BindsFromJson()
    {
        var r = JsonSerializer.Deserialize<UpdateTestDefinitionRequest>(TitrationBody, ApiOptions())!;
        Assert.Equal("Assay by titration", r.DisplayName);
        Assert.Equal(WorkflowType.Titration, r.WorkflowType);
        Assert.Equal(12, r.TitrantSolutionMasterId);
        Assert.Equal(5, r.TitrationIndicatorEntryId);
    }

    [Fact]
    public void CreateRequest_OmittedFieldsKeepTheirDefaults()
    {
        var r = JsonSerializer.Deserialize<CreateTestDefinitionRequest>("""{"code":"C1","displayName":"D1"}""", ApiOptions())!;
        Assert.Equal(WorkflowType.Observation, r.WorkflowType);
        Assert.Equal(EquationType.None, r.EquationType);
        Assert.Null(r.SectionId);
    }
}
