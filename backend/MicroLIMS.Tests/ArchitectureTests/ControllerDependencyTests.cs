using System.Reflection;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Persistence.DbContext;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace MicroLIMS.Tests.ArchitectureTests;

// Controllers map HTTP to Application services. None of them reaches the
// database itself - not through a constructor, and not through an
// action's [FromServices] parameter.
public class ControllerDependencyTests
{
    private static readonly Type[] DatabaseTypes = { typeof(MicroLimsDbContext), typeof(IMicroLimsDbContext) };

    public static TheoryData<string> Controllers()
    {
        var data = new TheoryData<string>();
        foreach (var t in typeof(MicroLIMS.API.Controllers.SampleController).Assembly.GetTypes()
                     .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
                     .OrderBy(t => t.FullName))
            data.Add(t.FullName!);
        return data;
    }

    [Theory]
    [MemberData(nameof(Controllers))]
    public void Controller_DoesNotTakeTheDbContext(string controllerName)
    {
        var controller = typeof(MicroLIMS.API.Controllers.SampleController).Assembly.GetType(controllerName)!;

        var constructorParameters = controller.GetConstructors().SelectMany(c => c.GetParameters());
        var actionParameters = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetParameters());

        var offending = constructorParameters.Concat(actionParameters)
            .Where(p => DatabaseTypes.Contains(p.ParameterType))
            .Select(p => $"{p.Member.Name}({p.Name})")
            .ToList();

        Assert.True(offending.Count == 0, $"{controller.Name} takes the database directly: {string.Join(", ", offending)}");
    }

    [Fact]
    public void Controllers_AreDiscovered() =>
        Assert.True(((IEnumerable<object[]>)Controllers()).Count() > 40);
}
