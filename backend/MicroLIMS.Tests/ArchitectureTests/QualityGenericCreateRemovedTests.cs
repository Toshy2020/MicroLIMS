using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace MicroLIMS.Tests.ArchitectureTests;

// CONF-230: a generic POST api/quality/{type} created CAPA, change-control and complaint
// root rows with no detail row and bypassed the typed services' permission and role rules.
// Every type is created only through its typed service route.
public class QualityGenericCreateRemovedTests
{
    private static readonly Assembly Api = typeof(MicroLIMS.API.Controllers.SampleController).Assembly;

    private static IEnumerable<(string Controller, string Action, string Route)> PostRoutes() =>
        Api.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t =>
            {
                var prefix = t.GetCustomAttribute<RouteAttribute>()?.Template ?? "";
                return t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetCustomAttributes<HttpPostAttribute>()
                        .Select(a => (t.Name, m.Name, (prefix + "/" + (a.Template ?? "")).Trim('/'))));
            });

    [Theory]
    [InlineData("Capa")]
    [InlineData("ChangeControl")]
    [InlineData("Complaint")]
    [InlineData("Deviation")]
    public void No_POST_route_can_bind_api_quality_type_as_a_generic_create(string type)
    {
        var generic = PostRoutes().Where(r =>
            r.Route.Equals("api/quality/{type}", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(generic.Count == 0,
            $"Generic create route for {type} still exists: " + string.Join(", ", generic.Select(g => $"{g.Controller}.{g.Action}")));
    }

    [Fact]
    public void QualityRecordsController_exposes_no_Create_action()
    {
        var controller = typeof(MicroLIMS.API.Controllers.QualityRecordsController);
        Assert.Null(controller.GetMethod("Create", BindingFlags.Public | BindingFlags.Instance));
    }
}
