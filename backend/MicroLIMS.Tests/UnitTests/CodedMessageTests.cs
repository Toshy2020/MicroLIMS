using System.Text.Json;
using System.Text.RegularExpressions;
using MicroLIMS.API.Middleware;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Competency;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Shared.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// F4 Task 4: a refusal that carries a message code reaches the client with the code, its parameters and the
// English sentence; one that does not looks exactly as before.
public class CodedMessageTests
{
    private sealed class NoCapture : IErrorCaptureService
    {
        public Task CaptureAsync(ErrorCaptureRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static async Task<(int Status, JsonElement Body)> RunAsync(Exception thrown)
    {
        var middleware = new ExceptionMiddleware(_ => throw thrown, NullLogger<ExceptionMiddleware>.Instance, new NoCapture());
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context);
        context.Response.Body.Position = 0;
        var text = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return (context.Response.StatusCode, JsonDocument.Parse(text).RootElement.Clone());
    }

    [Fact]
    public async Task Coded_business_rule_is_400_with_code_params_and_english_message()
    {
        var (status, body) = await RunAsync(new BusinessRuleException(
            MessageCodes.TestAlreadyTaken, "Already taken by Dana Lee.", new Dictionary<string, string> { ["name"] = "Dana Lee" }));

        Assert.Equal(400, status);
        Assert.Equal("Already taken by Dana Lee.", body.GetProperty("message").GetString());
        var detail = Assert.Single(body.GetProperty("errorDetails").EnumerateArray());
        Assert.Equal("Test.AlreadyTaken", detail.GetProperty("code").GetString());
        Assert.Equal("Dana Lee", detail.GetProperty("params").GetProperty("name").GetString());
        Assert.Equal("Already taken by Dana Lee.", detail.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Aggregate_refusal_returns_one_coded_detail_per_reason()
    {
        var (status, body) = await RunAsync(new BusinessRuleException(MessageCodes.CompetencyRecordingRefused, "A. B.")
        {
            ErrorDetails =
            [
                new ApiError(MessageCodes.CompetencyNotQualified, new Dictionary<string, string> { ["test"] = "A" }, "A."),
                new ApiError(MessageCodes.CompetencySopNotAcknowledged,
                    new Dictionary<string, string> { ["documentCode"] = "SOP-1", ["revision"] = "2" }, "B."),
            ],
        });

        Assert.Equal(400, status);
        Assert.Equal("A. B.", body.GetProperty("message").GetString());
        var details = body.GetProperty("errorDetails").EnumerateArray().ToList();
        Assert.Equal(new[] { "Competency.NotQualified", "Competency.SopNotAcknowledged" },
            details.Select(d => d.GetProperty("code").GetString()));
        Assert.Equal("A", details[0].GetProperty("params").GetProperty("test").GetString());
        Assert.Equal("SOP-1", details[1].GetProperty("params").GetProperty("documentCode").GetString());
    }

    [Fact]
    public void Competency_skip_carries_every_reason_and_the_first_code()
    {
        var skip = new CompetencySkipDto(7, "T", [
            new CompetencyRefusalReasonDto("Competency.NotQualified", new Dictionary<string, string> { ["test"] = "T" }, "One."),
            new CompetencyRefusalReasonDto("Competency.SopNotAcknowledged", new Dictionary<string, string>(), "Two."),
        ]);

        Assert.Equal("Competency.NotQualified", skip.Code);
        Assert.Equal("One. Two.", skip.Reason);
        Assert.Equal(2, skip.Reasons.Count);
    }

    [Fact]
    public async Task Coded_not_found_is_404_and_coded_access_denial_is_403()
    {
        var (notFound, nfBody) = await RunAsync(new NotFoundException(MessageCodes.TestAlreadyTaken, "Gone."));
        Assert.Equal(404, notFound);
        Assert.Equal("Test.AlreadyTaken", nfBody.GetProperty("errorDetails")[0].GetProperty("code").GetString());

        var (denied, deniedBody) = await RunAsync(new CodedAccessDeniedException(MessageCodes.TestNotInYourSection, "Not yours."));
        Assert.Equal(403, denied);
        Assert.Equal("Not yours.", deniedBody.GetProperty("message").GetString());
        var detail = deniedBody.GetProperty("errorDetails")[0];
        Assert.Equal("Test.NotInYourSection", detail.GetProperty("code").GetString());
        Assert.False(detail.TryGetProperty("params", out _));
    }

    [Fact]
    public async Task Coded_field_validation_keeps_field_errors_and_adds_the_code()
    {
        var (status, body) = await RunAsync(new FieldValidationException(
            MessageCodes.SampleDuplicateBatchAndControl, "Duplicate.", [new FieldError("controlNumber", "Duplicate.")]));

        Assert.Equal(400, status);
        Assert.Equal("controlNumber", body.GetProperty("fieldErrors")[0].GetProperty("field").GetString());
        Assert.Equal("Sample.DuplicateBatchAndControl", body.GetProperty("errorDetails")[0].GetProperty("code").GetString());
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(403)]
    [InlineData(0)]
    public async Task Uncoded_responses_have_no_errorDetails_key(int kind)
    {
        Exception thrown = kind switch
        {
            400 => new BusinessRuleException("Plain rule."),
            404 => new NotFoundException("Plain missing."),
            403 => new UnauthorizedAccessException("Plain denied."),
            _ => new FieldValidationException("Plain field.", [new FieldError("f", "Plain field.")])
        };

        var (_, body) = await RunAsync(thrown);

        Assert.False(body.TryGetProperty("errorDetails", out _));
        Assert.True(body.TryGetProperty("message", out _));
    }

    [Fact]
    public void A_refused_ActionCheck_throws_the_coded_exception_with_the_same_english_sentence()
    {
        var rule = Assert.Throws<BusinessRuleException>(() =>
            ActionCheck.No(MessageCodes.SampleCannotReviewTested, "You cannot review a sample you tested.").ThrowIfRefused());
        Assert.Equal("Sample.CannotReviewTested", rule.Code);
        Assert.Equal("You cannot review a sample you tested.", rule.Message);

        var denied = Assert.Throws<CodedAccessDeniedException>(() =>
            ActionCheck.Denied(MessageCodes.AuthRoleDoesNotIncludeAction, ActionCheck.RoleDoesNotIncludeAction).ThrowIfRefused());
        Assert.Equal("Auth.RoleDoesNotIncludeAction", denied.Code);

        var plain = Assert.Throws<BusinessRuleException>(() => ActionCheck.No("Plain.").ThrowIfRefused());
        Assert.Null(plain.Code);
    }

    [Fact]
    public void Action_availability_sends_reasonCode_only_when_it_has_one()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var coded = JsonSerializer.Serialize(new ActionAvailability("x", false, "No.", "Test.AlreadyTaken",
            new Dictionary<string, string> { ["name"] = "Dana" }), options);
        var plain = JsonSerializer.Serialize(new ActionAvailability("x", false, "No."), options);

        Assert.Contains("\"reasonCode\":\"Test.AlreadyTaken\"", coded);
        Assert.Contains("\"reasonParams\":{\"name\":\"Dana\"}", coded);
        Assert.DoesNotContain("reasonCode", plain);
        Assert.DoesNotContain("reasonParams", plain);
    }

    // Reads the frontend's template files straight from the repository, so a code added here without both
    // templates, or a template that names a parameter the server does not send, fails in this suite. (The
    // alternative, a generated JSON checked by a frontend test, would need a build step to stay current.)
    [Fact]
    public void Server_message_templates_cover_all_params()
    {
        var problems = new List<string>();
        var codes = typeof(MessageCodes).GetFields()
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(codes);
        Assert.Equal(codes.OrderBy(c => c), MessageCodes.ParameterNames.Keys.OrderBy(c => c));
        Assert.All(codes, c => Assert.Matches(@"^[A-Z][A-Za-z0-9]*\.[A-Z][A-Za-z0-9]*$", c));

        foreach (var language in new[] { "en", "ar" })
        {
            var path = Path.Combine(LocalesDirectory(), language, "serverMessages.json");
            var templates = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;

            foreach (var code in codes)
            {
                if (!templates.TryGetValue(code, out var template)) { problems.Add($"{language}: no template for {code}"); continue; }
                var used = PlaceholdersOf(template);
                var expected = MessageCodes.ParameterNames[code].OrderBy(n => n, StringComparer.Ordinal).ToArray();
                if (!used.SequenceEqual(expected))
                    problems.Add($"{language}: {code} uses [{string.Join(",", used)}] but the server sends [{string.Join(",", expected)}]");
            }

            foreach (var extra in templates.Keys.Except(codes))
                problems.Add($"{language}: template {extra} has no code in MessageCodes");
        }

        Assert.Empty(problems);
    }

    [Theory]
    [InlineData("Taken by {{name}}.", "name")]
    [InlineData("{{b}} and {{a}} and {{b}}", "a,b")]
    [InlineData("No placeholders.", "")]
    public void Placeholder_reader_finds_each_distinct_name(string template, string expected) =>
        Assert.Equal(expected, string.Join(",", PlaceholdersOf(template)));

    private static string[] PlaceholdersOf(string template) =>
        Regex.Matches(template, @"\{\{\s*([^,}\s]+)[^}]*\}\}").Select(m => m.Groups[1].Value)
            .Distinct().OrderBy(n => n, StringComparer.Ordinal).ToArray();

    private static string LocalesDirectory([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "..", "frontend", "src", "locales"));
}
