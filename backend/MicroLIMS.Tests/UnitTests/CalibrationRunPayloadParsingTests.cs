using System.Text.Json;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// The create endpoint parses its multipart "payload" by hand; the frontend
// sends enums as strings and decimals as typed, exactly like JSON bodies.
public class CalibrationRunPayloadParsingTests
{
    [Fact]
    public void Payload_WithStringEnumsAndDecimals_Parses()
    {
        const string json = """
        {"testDefinitionId":3,"equipmentId":7,"calibrationStandardMaterialId":11,"icvStandardMaterialId":null,
         "calibrationAt":"2026-09-19T07:15:00.000Z","password":"x","comment":null,
         "analytes":[{"testAnalyteId":5,"correlationValue":0.9995,"correlationType":"RSquared","numberOfStandards":5,
           "lowestStandardMgPerL":0.1,"highestStandardMgPerL":10.0,
           "checks":[{"checkType":"Icv","sequencePosition":1,"nominalMgPerL":5.0,"measuredMgPerL":5.5},
                     {"checkType":"Blank","sequencePosition":2,"nominalMgPerL":null,"measuredMgPerL":0.001}]}]}
        """;

        var request = JsonSerializer.Deserialize<CreateCalibrationRunRequest>(json, CalibrationRunController.PayloadJsonOptions)!;

        var analyte = Assert.Single(request.Analytes!);
        Assert.Equal(CorrelationType.RSquared, analyte.CorrelationType);
        Assert.Equal(0.9995m, analyte.CorrelationValue);
        Assert.Equal(CalibrationCheckType.Icv, analyte.Checks![0].CheckType);
        Assert.Equal(5.5m, analyte.Checks[0].MeasuredMgPerL);
        Assert.Equal(CalibrationCheckType.Blank, analyte.Checks[1].CheckType);
        Assert.Equal(DateTimeKind.Utc, request.CalibrationAt.Kind);
    }
}
