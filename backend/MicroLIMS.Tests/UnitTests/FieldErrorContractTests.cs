using System.Text.Json;
using MicroLIMS.API.Filters;
using MicroLIMS.API.Json;
using MicroLIMS.API.Middleware;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class FieldErrorContractTests
{
    private sealed class NoCapture : IErrorCaptureService
    {
        public Task CaptureAsync(ErrorCaptureRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private static JsonSerializerOptions ApiOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApiJsonOptions.Configure(options);
        return options;
    }

    private static async Task<(int Status, string Body)> RunMiddlewareAsync(Exception thrown)
    {
        var middleware = new ExceptionMiddleware(
            _ => throw thrown,
            NullLogger<ExceptionMiddleware>.Instance,
            new NoCapture());
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return (context.Response.StatusCode, body);
    }

    [Fact]
    public void Fail_WithoutFieldErrors_SerialisesExactlyAsBefore()
    {
        const string expectedWithErrors = """{"success":false,"message":"Validation failed.","data":null,"errors":["Name is required."]}""";
        const string expectedWithoutErrors = """{"success":false,"message":"Something broke.","data":null,"errors":null}""";

        var withErrorsJson = JsonSerializer.Serialize(
            ApiResponse<object>.Fail("Validation failed.", new List<string> { "Name is required." }),
            ApiOptions());
        var withoutErrorsJson = JsonSerializer.Serialize(
            ApiResponse<object>.Fail("Something broke."),
            ApiOptions());

        Assert.Equal(expectedWithErrors, withErrorsJson);
        Assert.Equal(expectedWithoutErrors, withoutErrorsJson);
    }

    [Fact]
    public void ModelStateError_ProducesCamelCaseFieldAndKeepsErrorsList()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("request.BatchNumber", "Batch number is required.");
        modelState.AddModelError("ControlNumber", "Control number is invalid.");
        modelState.AddModelError("request.Items[0].SampleName", "Sample name is required.");

        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor(),
            modelState);

        var executingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: new object());

        var filter = new ValidationFilter();
        filter.OnActionExecuting(executingContext);

        var badRequest = Assert.IsType<BadRequestObjectResult>(executingContext.Result);
        var json = JsonSerializer.Serialize(badRequest.Value, ApiOptions());

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("Validation failed.", root.GetProperty("message").GetString());

        var errors = root.GetProperty("errors").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(3, errors.Count);
        Assert.Contains("Batch number is required.", errors);
        Assert.Contains("Control number is invalid.", errors);
        Assert.Contains("Sample name is required.", errors);

        var fieldErrors = root.GetProperty("fieldErrors").EnumerateArray()
            .ToDictionary(
                e => e.GetProperty("field").GetString() ?? string.Empty,
                e => e.GetProperty("message").GetString());
        Assert.Equal(3, fieldErrors.Count);
        Assert.Equal("Batch number is required.", fieldErrors["batchNumber"]);
        Assert.Equal("Control number is invalid.", fieldErrors["controlNumber"]);
        Assert.Equal("Sample name is required.", fieldErrors["items[0].sampleName"]);
    }

    [Fact]
    public async Task FieldValidationException_Returns400_WithFieldErrorsAndRows()
    {
        var exception = new FieldValidationException(
            "Some rows need correction.",
            new List<FieldError>
            {
                new("batchNumber", "Batch number is required.", Row: 0),
                new("controlNumber", "Duplicate control number.", Row: 2),
                new("category", "Category is invalid.")
            });

        var (status, body) = await RunMiddlewareAsync(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, status);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("Some rows need correction.", root.GetProperty("message").GetString());

        var errors = root.GetProperty("errors").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(
            new[] { "Batch number is required.", "Duplicate control number.", "Category is invalid." },
            errors);

        var fieldErrors = root.GetProperty("fieldErrors").EnumerateArray().ToList();
        Assert.Equal(3, fieldErrors.Count);
        Assert.Equal("batchNumber", fieldErrors[0].GetProperty("field").GetString());
        Assert.Equal("Batch number is required.", fieldErrors[0].GetProperty("message").GetString());
        Assert.Equal(0, fieldErrors[0].GetProperty("row").GetInt32());

        Assert.Equal("controlNumber", fieldErrors[1].GetProperty("field").GetString());
        Assert.Equal("Duplicate control number.", fieldErrors[1].GetProperty("message").GetString());
        Assert.Equal(2, fieldErrors[1].GetProperty("row").GetInt32());

        Assert.Equal("category", fieldErrors[2].GetProperty("field").GetString());
        Assert.Equal("Category is invalid.", fieldErrors[2].GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, fieldErrors[2].GetProperty("row").ValueKind);
    }

    [Fact]
    public async Task BusinessRuleException_StillReturns400_NoFieldErrors()
    {
        var (status, body) = await RunMiddlewareAsync(new BusinessRuleException("Lot is expired."));

        Assert.Equal(StatusCodes.Status400BadRequest, status);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("Lot is expired.", root.GetProperty("message").GetString());
        Assert.False(root.TryGetProperty("fieldErrors", out _));
    }
}
