using MicroLIMS.API.Extensions;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Persistence.DbContext;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// The unit tests construct services by hand, so none of them would notice
// a constructor dependency the real container cannot supply. This builds
// the container the way Program.cs does and has it check every
// registration - services and controllers - up front.
public class DependencyInjectionCompositionTests
{
    private static IServiceProvider BuildProvider()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Host.UseDefaultServiceProvider(o =>
        {
            o.ValidateOnBuild = true;
            o.ValidateScopes = true;
        });
        builder.Services.AddSingleton(new JwtSettings(new string('k', 64), "issuer", "audience", TimeSpan.FromMinutes(15)));
        builder.Services.AddMicroLimsDbContext(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(MicroLIMS.API.Controllers.SampleController).Assembly)
            .AddControllersAsServices();

        return builder.Build().Services;
    }

    [Fact]
    public void EveryRegistrationAndController_CanBeConstructed()
    {
        Assert.NotNull(BuildProvider());
    }

    [Fact]
    public void TheApplicationInterface_IsTheSameRequestScopedContext()
    {
        var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var concrete = scope.ServiceProvider.GetRequiredService<MicroLimsDbContext>();
        var abstraction = scope.ServiceProvider.GetRequiredService<IMicroLimsDbContext>();

        Assert.Same(concrete, abstraction);
    }

    // The container must build MicroLimsDbContext with the registered
    // TimeProvider, so audit rows use the application clock (CLAUDE.md
    // Rule 3.3.2) rather than the context's System fallback.
    [Fact]
    public async Task TheContainerContext_StampsAuditRowsWithTheRegisteredClock()
    {
        var fixedNow = new DateTimeOffset(2031, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(new JwtSettings(new string('k', 64), "issuer", "audience", TimeSpan.FromMinutes(15)));
        var store = Guid.NewGuid().ToString();
        builder.Services.AddMicroLimsDbContext(o => o.UseInMemoryDatabase(store));
        builder.Services.AddApplicationServices(builder.Configuration);
        // Registered last, so it is the TimeProvider the container resolves.
        builder.Services.AddSingleton<TimeProvider>(new MicroLIMS.Tests.Fixtures.FixedTimeProvider(fixedNow));
        var provider = builder.Build().Services;

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MicroLimsDbContext>();
            db.CurrentUserId = 1;
            db.Organisms.Add(new MicroLIMS.Domain.Entities.Organism { ScientificName = "DI clock organism" });
            await db.SaveChangesAsync();
        }

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MicroLimsDbContext>();
            var audit = await db.AuditLogs.AsNoTracking().SingleAsync(a => a.EntityName == "Organism" && a.Action == "Create");
            Assert.Equal(fixedNow.UtcDateTime, audit.Timestamp);
        }
    }
}
