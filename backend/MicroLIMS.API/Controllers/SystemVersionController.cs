using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public sealed record SystemVersionResponse(string Version, string Environment);

[ApiController]
[Route("api/system/version")]
[Authorize]
public class SystemVersionController : ControllerBase
{
    private readonly IHostEnvironment _environment;

    public SystemVersionController(IHostEnvironment environment) => _environment = environment;

    [HttpGet]
    public IActionResult Get() =>
        Ok(ApiResponse<SystemVersionResponse>.Ok(new SystemVersionResponse(ReadVersion(), _environment.EnvironmentName)));

    // The assembly's informational version exactly as built.
    public static string ReadVersion()
    {
        var assembly = typeof(SystemVersionController).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(informational) ? assembly.GetName().Version?.ToString() ?? "unknown" : informational;
    }
}
