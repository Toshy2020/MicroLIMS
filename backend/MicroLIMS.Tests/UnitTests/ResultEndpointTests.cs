using System.Reflection;
using Microsoft.AspNetCore.Mvc.Routing;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Interfaces;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Results are recorded only through TestWorkflowEngine, which gates entry
// on the order's workflow step and writes the reporting projection. The
// removed POST/PUT /api/results path let any role add a result to any test
// order in any state - including returning an Approved order to
// ResultEntered - so it must not come back without those same gates.
public class ResultEndpointTests
{
    [Fact]
    public void ResultController_ExposesNoWriteRoutes()
    {
        var writeRoutes = typeof(ResultController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>())
            .SelectMany(a => a.HttpMethods)
            .Where(verb => verb != "GET")
            .ToList();

        Assert.Empty(writeRoutes);
    }

    [Fact]
    public void ResultService_IsReadOnly()
    {
        var writeMethods = typeof(IResultService).GetMethods()
            .Where(m => !m.Name.StartsWith("Get", StringComparison.Ordinal))
            .Select(m => m.Name)
            .ToList();

        Assert.Empty(writeMethods);
    }
}
