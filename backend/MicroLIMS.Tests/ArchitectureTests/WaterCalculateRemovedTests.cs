using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace MicroLIMS.Tests.ArchitectureTests;

// CONF-216: POST api/water/calculate was removed (user decision, 2026-11-20).
// No test host exists for HTTP routing, so assert that no action on any
// controller still maps that route (a request would then get 404/405).
public class WaterCalculateRemovedTests
{
    [Fact]
    public void NoControllerActionMapsWaterCalculateRoute()
    {
        var apiAssembly = typeof(MicroLIMS.API.Controllers.WaterController).Assembly;
        var offenders = new List<string>();
        foreach (var type in apiAssembly.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t)))
        {
            var prefix = type.GetCustomAttribute<RouteAttribute>()?.Template ?? "";
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                foreach (var http in method.GetCustomAttributes<HttpMethodAttribute>())
                {
                    var full = (prefix + "/" + (http.Template ?? "")).Trim('/').ToLowerInvariant();
                    if (full == "api/water/calculate")
                        offenders.Add($"{type.Name}.{method.Name}");
                }
        }
        Assert.Empty(offenders);
    }
}
