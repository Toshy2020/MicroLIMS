using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace MicroLIMS.Tests.ArchitectureTests;

// The API's contract is its own types, not the database's.
//
// Requests bind to purpose-made request types, never to a domain entity:
// binding an entity lets a client set any column on it (ids, ownership,
// status, audit fields) - OWASP API3, mass assignment.
//
// Responses: some endpoints still serialize entities, so the JSON follows
// the schema and whatever EF happened to load. entity-responses.txt lists
// the service methods behind the controllers that return entities today;
// it may only shrink. A method moved to a response DTO comes off the list,
// and a new method cannot join it.
public class ApiContractTests
{
    private const string AllowListFile = "entity-responses.txt";

    private static readonly Assembly Domain = typeof(MicroLIMS.Domain.Entities.Sample).Assembly;

    private static bool IsOrContainsEntity(Type t) =>
        t.Assembly == Domain && t.Namespace == "MicroLIMS.Domain.Entities"
        || (t.IsGenericType && t.GetGenericArguments().Any(IsOrContainsEntity))
        || (t.IsArray && IsOrContainsEntity(t.GetElementType()!));

    [Fact]
    public void NoAction_BindsADomainEntity()
    {
        var offenders = typeof(MicroLIMS.API.Controllers.SampleController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(m => m.GetParameters().Select(p => (Method: m, Param: p)))
            .Where(x => IsOrContainsEntity(x.Param.ParameterType))
            .Select(x => $"{x.Method.DeclaringType!.Name}.{x.Method.Name}({x.Param.ParameterType.Name} {x.Param.Name})")
            .ToList();

        Assert.True(offenders.Count == 0, "Bind a request DTO instead:\n" + string.Join("\n", offenders));
    }

    private static List<string> EntityReturningServiceMethods()
    {
        var application = typeof(MicroLIMS.Application.Services.SampleSummaryService).Assembly;
        var services = typeof(MicroLIMS.API.Controllers.SampleController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType))
            .Where(t => t.Assembly == application)
            .ToHashSet();

        return services
            .SelectMany(s => s.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => IsOrContainsEntity(m.ReturnType))
                .Select(m => $"{s.Name}.{m.Name}"))
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    [Fact]
    public void EntityResponses_OnlyShrink()
    {
        var path = Path.Combine(Path.GetDirectoryName(ThisFile())!, AllowListFile);
        var actual = EntityReturningServiceMethods();
        if (Environment.GetEnvironmentVariable("MICROLIMS_WRITE_ENTITY_RESPONSES") == "1")
            File.WriteAllLines(path, actual);

        var allowed = File.ReadAllLines(path).Where(l => l.Length > 0).ToHashSet();
        var added = actual.Where(m => !allowed.Contains(m)).ToList();
        var converted = allowed.Where(m => !actual.Contains(m)).ToList();

        Assert.True(added.Count == 0, "Return a response DTO, not an entity:\n" + string.Join("\n", added));
        Assert.True(converted.Count == 0, $"No longer return entities - remove them from {AllowListFile}:\n" + string.Join("\n", converted));
    }

    private static string ThisFile([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;
}
